using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using GPMH.Application.Common.Interfaces;
using GPMH.Application.DTOs.Formula;
using GPMH.Application.DTOs.Printing;
using GPMH.Domain.Entities.Assessments;
using GPMH.Domain.Entities.Auditing;
using GPMH.Domain.Entities.Taxation;
using GPMH.Domain.Entities.Tenancy;
using GPMH.Domain.Services;
using Microsoft.EntityFrameworkCore;

namespace GPMH.Infrastructure.Persistence.Repositories;

public sealed class PrintDataRepository(
    AppDbContext dbContext,
    IExtractVerificationTokenService tokenService,
    IQrCodeGeneratorService qrCodeGenerator,
    IPiiProtectionService piiProtectionService,
    ICurrentUserService currentUserService)
    : IPrintDataRepository
{
    private static readonly JsonSerializerOptions JsonWebOptions = new(JsonSerializerDefaults.Web);

    public async Task<IReadOnlyList<Namuna8ExtractPdfDocumentModel>> LoadNamuna8ExtractsAsync(
        Guid tenantId,
        Guid? propertyId,
        string? wardNo,
        Guid? assessmentYearId,
        IReadOnlyCollection<Guid>? propertyIds,
        string frontendBaseUrl,
        CancellationToken cancellationToken)
    {
        PrintContextData ctx = await LoadTenantPrintContextAsync(
            tenantId,
            propertyId,
            wardNo,
            assessmentYearId,
            propertyIds,
            loadProtectedImages: true,
            cancellationToken).ConfigureAwait(false);

        if (ctx.Properties.Count == 0)
        {
            return [];
        }

        DateTimeOffset issuedUtc = DateTimeOffset.UtcNow;
        string issuedDateDisplay = issuedUtc.ToLocalTime().ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
        string baseUrl = frontendBaseUrl.TrimEnd('/');

        List<Namuna8ExtractPdfDocumentModel> results = new(ctx.Properties.Count);
        for (int i = 0; i < ctx.Properties.Count; i++)
        {
            Property p = ctx.Properties[i];
            ctx.OwnersByProp.TryGetValue(p.Id, out List<OwnerJoinRow>? propOwners);
            propOwners ??= [];
            ctx.FloorsByProp.TryGetValue(p.Id, out List<PropertyConstruction>? propFloors);
            propFloors ??= [];
            ctx.TaxCalcByProp.TryGetValue(p.Id, out PropertyTaxCalc? taxCalc);

            SnapshotMetrics metrics = ExtractSnapshotMetrics(p, propFloors, taxCalc, ctx.Constructions, ctx.Usages);

            string token = tokenService.GenerateToken(new ExtractVerifyPayloadDto(
                TenantId: tenantId,
                PropertyId: p.Id,
                AssessmentYearId: ctx.YearId,
                IssuedAtUtc: issuedUtc));

            string verifyUrl = $"{baseUrl}/verify/extract/{token}";
            string verifyQrSvg = qrCodeGenerator.GenerateSvg(verifyUrl, pixelsPerModule: 3);

            string? upiPayUri = BuildUpiPayUri(ctx.UpiId, ctx.PayeeName, p.IsTaxExempt ? 0m : metrics.TotalAnnualTax, $"{p.WardNo}/{p.PropertyNo}");

            OwnerJoinRow? primaryOwner = propOwners.FirstOrDefault();
            string ownersMr = propOwners.Count == 0
                ? "-"
                : string.Join(", ", propOwners.Select(o =>
                    propOwners.Count > 1
                        ? $"{o.FullNameMr} ({o.OwnershipSharePct:0.##}%)"
                        : o.FullNameMr));
            string ownersEn = propOwners.Count == 0
                ? "-"
                : string.Join(", ", propOwners.Select(o => o.FullNameEn));

            string? maskedMobile = string.IsNullOrWhiteSpace(primaryOwner?.MobileNo) || primaryOwner.MobileNo.Length < 4
                ? null
                : $"XXXXXX{primaryOwner.MobileNo[^4..]}";

            string occupier = string.IsNullOrWhiteSpace(p.OccupierName)
                ? (primaryOwner?.FullNameMr ?? "स्वतः (Self)")
                : p.OccupierName;

            string constructionDesc = propFloors.Count > 0
                ? string.Join("; ", propFloors.Select(f =>
                {
                    string ctLabel = ctx.Constructions.TryGetValue(f.ConstructionTypeCode, out ConstructionTypeMaster? ct)
                        ? ct.TypeName
                        : f.ConstructionTypeCode;
                    string uLabel = ctx.Usages.TryGetValue(f.UsageCode, out UsageWeightage? uw)
                        ? uw.UsageName
                        : f.UsageCode;
                    return $"{f.FloorLabel}: {ctLabel} ({uLabel})";
                }))
                : (p.OpenPlotAreaSqFt > 0m ? "खुली जागा / भूखंड" : "नोंदणीकृत मिळकत");

            string descriptionMr = string.IsNullOrWhiteSpace(p.PropertyDescription)
                ? constructionDesc
                : $"{p.PropertyDescription} — {constructionDesc}";

            string builtYearsDisplay = propFloors.Count > 0
                ? string.Join(", ", propFloors.Select(f => f.ConstructionStartYear.ToString(CultureInfo.InvariantCulture)).Distinct())
                : "-";

            decimal totalAreaSqM = Math.Round(p.TotalAreaSqFt * CapitalValueTaxCalculator.SqFtToSqM, 2, MidpointRounding.AwayFromZero);

            string remarks = p.IsTaxExempt
                ? "वैधानिक कर माफी लागू (Tax Exempt)"
                : $"नियमित कर आकारणी (वर्ष {ctx.YearLabel})";
            if (string.Equals(p.Status, "Partitioned", StringComparison.OrdinalIgnoreCase))
            {
                remarks = $"विभाजित मिळकत (Partitioned) • {remarks}";
            }

            results.Add(new Namuna8ExtractPdfDocumentModel(
                TenantId: tenantId,
                PropertyId: p.Id,
                AssessmentYearId: ctx.YearId,
                SrNo: i + 1,
                LgdCode: ctx.LgdCode,
                PanchayatNameEn: ctx.PanchayatNameEn,
                PanchayatNameMr: ctx.PanchayatNameMr,
                VillageNameMr: ctx.PanchayatNameMr,
                TehsilName: ctx.TehsilName,
                DistrictName: ctx.DistrictName,
                Pincode: ctx.Pincode,
                AssessmentYearLabel: ctx.YearLabel,
                RatePeriodName: ctx.RatePeriodName,
                DocumentRefNo: $"GPMH-{ctx.LgdCode}-{ctx.YearLabel}-W{p.WardNo}-P{p.PropertyNo}",
                IssuedDateDisplay: issuedDateDisplay,
                WardNo: p.WardNo,
                PropertyNo: p.PropertyNo,
                StreetOrLaneName: p.StreetOrLaneName,
                CitySurveyOrGutNo: p.CitySurveyOrGutNo,
                OwnersSummaryMr: ownersMr,
                OwnersSummaryEn: ownersEn,
                PrimaryOwnerAadhaarMasked: string.IsNullOrWhiteSpace(primaryOwner?.AadhaarLast4)
                    ? null
                    : $"XXXX-XXXX-{primaryOwner.AadhaarLast4}",
                PrimaryOwnerMobileMasked: maskedMobile,
                OccupierName: occupier,
                PropertyDescriptionMr: descriptionMr,
                ConstructionYearsDisplay: builtYearsDisplay,
                BuiltUpAreaSqFt: p.BuiltUpAreaSqFt,
                OpenPlotAreaSqFt: p.OpenPlotAreaSqFt,
                TotalAreaSqFt: p.TotalAreaSqFt,
                TotalAreaSqM: totalAreaSqM,
                OpenLandRrRate: metrics.OpenLandRrRate,
                BuildingLandRate: metrics.BuildingLandRate,
                ConstructionRrRate: metrics.ConstructionRrRate,
                DepreciationPercentDisplay: metrics.DepreciationDisplay,
                UsageWeightageDisplay: metrics.UsageWeightageDisplay,
                TotalCapitalValue: metrics.CapitalValue,
                TaxRateDisplay: metrics.TaxRateDisplay,
                BuildingTaxComponent: metrics.BuildingTaxComponent,
                LightingTax: metrics.LightingTax,
                HealthTax: metrics.HealthTax,
                WaterTax: metrics.WaterTax,
                SanitationTax: metrics.SanitationTax,
                ToiletTax: metrics.ToiletTax,
                OtherTax: metrics.OtherTax,
                TotalAnnualTax: p.IsTaxExempt ? 0m : metrics.TotalAnnualTax,
                RevisionHouseTax: null,
                RevisionLightingTax: null,
                RevisionHealthTax: null,
                RevisionWaterTax: null,
                RevisionTotalTax: null,
                Remarks: remarks,
                WaterConnectionLabelMr: FormatWaterLabelMr(p.WaterConnectionCode),
                IsTaxExempt: p.IsTaxExempt,
                Status: p.Status,
                BoundaryEast: p.BoundaryEast,
                BoundaryWest: p.BoundaryWest,
                BoundaryNorth: p.BoundaryNorth,
                BoundarySouth: p.BoundarySouth,
                GramsevakName: ctx.GramsevakName,
                SarpanchName: ctx.SarpanchName,
                OperatorName: ctx.OperatorName,
                VerifyToken: token,
                VerifyUrl: verifyUrl,
                VerifyQrSvg: verifyQrSvg,
                UpiId: ctx.UpiId,
                UpiPayUri: upiPayUri,
                GramsevakSignatureBytes: ctx.GramsevakSigBytes,
                SarpanchSignatureBytes: ctx.SarpanchSigBytes,
                OperatorSignatureBytes: ctx.OperatorSigBytes,
                OfficialStampBytes: ctx.OfficialStampBytes));
        }

        return results;
    }

    public async Task<IReadOnlyList<DigitalNameplateItemData>> LoadNameplatesAsync(
        Guid tenantId,
        Guid? propertyId,
        string? wardNo,
        IReadOnlyCollection<Guid>? propertyIds,
        string frontendBaseUrl,
        CancellationToken cancellationToken)
    {
        PrintContextData ctx = await LoadTenantPrintContextAsync(
            tenantId,
            propertyId,
            wardNo,
            assessmentYearId: null,
            propertyIds,
            loadProtectedImages: false,
            cancellationToken).ConfigureAwait(false);

        if (ctx.Properties.Count == 0)
        {
            return [];
        }

        DateTimeOffset issuedUtc = DateTimeOffset.UtcNow;
        string baseUrl = frontendBaseUrl.TrimEnd('/');
        List<DigitalNameplateItemData> plates = new(ctx.Properties.Count);

        foreach (Property p in ctx.Properties)
        {
            ctx.OwnersByProp.TryGetValue(p.Id, out List<OwnerJoinRow>? propOwners);
            propOwners ??= [];
            ctx.FloorsByProp.TryGetValue(p.Id, out List<PropertyConstruction>? propFloors);
            propFloors ??= [];
            ctx.TaxCalcByProp.TryGetValue(p.Id, out PropertyTaxCalc? taxCalc);

            OwnerJoinRow? primaryOwner = propOwners.FirstOrDefault();
            string coOwnersMr = propOwners.Count > 1
                ? string.Join(", ", propOwners.Skip(1).Select(o => o.FullNameMr))
                : string.Empty;

            decimal annualTax = p.IsTaxExempt ? 0m : (taxCalc?.TotalTax ?? 0m);
            string token = tokenService.GenerateToken(new ExtractVerifyPayloadDto(
                TenantId: tenantId,
                PropertyId: p.Id,
                AssessmentYearId: ctx.YearId,
                IssuedAtUtc: issuedUtc));

            string verifyUrl = $"{baseUrl}/verify/extract/{token}";
            string verifyQrSvg = qrCodeGenerator.GenerateSvg(verifyUrl, pixelsPerModule: 3);
            string? upiPayUri = BuildUpiPayUri(ctx.UpiId, ctx.PayeeName, annualTax, $"{p.WardNo}/{p.PropertyNo}");
            string? upiQrSvg = string.IsNullOrWhiteSpace(upiPayUri) ? null : qrCodeGenerator.GenerateSvg(upiPayUri, pixelsPerModule: 3);

            string usageStr = propFloors.Count > 0
                ? string.Join(", ", propFloors.Select(f =>
                {
                    string ctLabel = ctx.Constructions.TryGetValue(f.ConstructionTypeCode, out ConstructionTypeMaster? ct)
                        ? ct.TypeName
                        : f.ConstructionTypeCode;
                    string uLabel = ctx.Usages.TryGetValue(f.UsageCode, out UsageWeightage? uw)
                        ? uw.UsageName
                        : f.UsageCode;
                    return $"{uLabel} ({ctLabel})";
                }).Distinct())
                : (p.OpenPlotAreaSqFt > 0m ? "खुला भूखंड" : "मालमत्ता");

            decimal totalSqM = Math.Round(p.TotalAreaSqFt * CapitalValueTaxCalculator.SqFtToSqM, 2, MidpointRounding.AwayFromZero);

            plates.Add(new DigitalNameplateItemData(
                PropertyId: p.Id,
                LgdCode: ctx.LgdCode,
                PanchayatNameEn: ctx.PanchayatNameEn,
                PanchayatNameMr: ctx.PanchayatNameMr,
                TehsilName: ctx.TehsilName,
                DistrictName: ctx.DistrictName,
                Pincode: ctx.Pincode,
                WardNo: p.WardNo,
                PropertyNo: p.PropertyNo,
                OwnerNameMr: primaryOwner?.FullNameMr ?? "-",
                OwnerNameEn: primaryOwner?.FullNameEn ?? "-",
                CoOwnerNamesMr: string.IsNullOrWhiteSpace(coOwnersMr) ? null : coOwnersMr,
                OccupierLabelMr: string.IsNullOrWhiteSpace(p.OccupierName) ? "स्वतः (Self)" : p.OccupierName,
                ConstructionAndUsageMr: usageStr,
                BuiltUpAreaSqFt: p.BuiltUpAreaSqFt,
                OpenPlotAreaSqFt: p.OpenPlotAreaSqFt,
                TotalAreaSqFt: p.TotalAreaSqFt,
                TotalAreaSqM: totalSqM,
                TotalAnnualTax: annualTax,
                IsTaxExempt: p.IsTaxExempt,
                UniqueCode: $"GPMH-{ctx.LgdCode}-{p.WardNo}-{p.PropertyNo}",
                VerifyToken: token,
                VerifyUrl: verifyUrl,
                VerifyQrSvg: verifyQrSvg,
                UpiId: ctx.UpiId,
                UpiPayUri: upiPayUri,
                UpiQrSvg: upiQrSvg));
        }

        return plates;
    }

    public async Task<VerifiedExtractPublicDto?> LoadVerifiedExtractPublicAsync(
        Guid tenantId,
        Guid propertyId,
        Guid assessmentYearId,
        string token,
        DateTimeOffset issuedAtUtc,
        CancellationToken cancellationToken)
    {
        // Note: This path executes anonymously ONLY AFTER cryptographic HMAC verification of (tenantId, propertyId).
        Property? prop = await dbContext.Properties
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == propertyId && p.TenantId == tenantId && p.IsActive, cancellationToken)
            .ConfigureAwait(false);

        if (prop is null)
        {
            return null;
        }

        Tenant? tenant = await dbContext.Tenants
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == tenantId, cancellationToken)
            .ConfigureAwait(false);

        if (tenant is null)
        {
            return null;
        }

        GramPanchayat? gp = await dbContext.GramPanchayats
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(g => g.TenantId == tenantId, cancellationToken)
            .ConfigureAwait(false);

        AssessmentYear? year = null;
        if (assessmentYearId != Guid.Empty)
        {
            year = await dbContext.AssessmentYears
                .IgnoreQueryFilters()
                .AsNoTracking()
                .FirstOrDefaultAsync(y => y.TenantId == tenantId && y.Id == assessmentYearId, cancellationToken)
                .ConfigureAwait(false);
        }

        year ??= await dbContext.AssessmentYears
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(y => y.TenantId == tenantId)
            .OrderByDescending(y => y.IsActive)
            .ThenByDescending(y => y.StartYear)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        List<OwnerJoinRow> ownersRaw = await (
            from po in dbContext.PropertyOwners.IgnoreQueryFilters().AsNoTracking()
            join c in dbContext.Citizens.IgnoreQueryFilters().AsNoTracking() on po.CitizenId equals c.Id
            where po.TenantId == tenantId && po.PropertyId == propertyId && po.IsActive
            select new OwnerJoinRow(
                po.PropertyId,
                c.FullNameMr,
                c.FullNameEn,
                null, // Strict DPDP minimization on public endpoint: NEVER select MobileNo
                null, // Strict DPDP minimization on public endpoint: NEVER select Aadhaar
                po.IsPrimaryOwner,
                po.OwnershipSharePct))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        OwnerJoinRow? primaryOwner = ownersRaw
            .OrderByDescending(o => o.IsPrimaryOwner)
            .ThenByDescending(o => o.OwnershipSharePct)
            .FirstOrDefault();

        decimal totalTax = 0m;
        if (!prop.IsTaxExempt && year is not null)
        {
            PropertyTaxCalc? tc = await dbContext.PropertyTaxCalcs
                .IgnoreQueryFilters()
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.TenantId == tenantId && t.PropertyId == propertyId && t.AssessmentYearId == year.Id, cancellationToken)
                .ConfigureAwait(false);
            totalTax = tc?.TotalTax ?? 0m;
        }

        decimal totalSqM = Math.Round(prop.TotalAreaSqFt * CapitalValueTaxCalculator.SqFtToSqM, 2, MidpointRounding.AwayFromZero);

        return new VerifiedExtractPublicDto(
            IsValid: true,
            Token: token,
            TenantId: tenantId,
            PropertyId: propertyId,
            LgdCode: gp?.LgdCode ?? tenant.LgdCode,
            PanchayatNameEn: gp?.PanchayatNameEn ?? tenant.NameEn,
            PanchayatNameMr: gp?.PanchayatNameMr ?? tenant.NameMr,
            TehsilName: gp?.TehsilName ?? "-",
            DistrictName: gp?.DistrictName ?? "-",
            AssessmentYearLabel: year?.YearLabel ?? $"{DateTime.UtcNow.Year}-{(DateTime.UtcNow.Year + 1) % 100:00}",
            WardNo: prop.WardNo,
            PropertyNo: prop.PropertyNo,
            PrimaryOwnerNameMr: primaryOwner?.FullNameMr ?? "-",
            PrimaryOwnerNameEn: primaryOwner?.FullNameEn ?? "-",
            BuiltUpAreaSqFt: prop.BuiltUpAreaSqFt,
            OpenPlotAreaSqFt: prop.OpenPlotAreaSqFt,
            TotalAreaSqFt: prop.TotalAreaSqFt,
            TotalAreaSqM: totalSqM,
            TotalAnnualTax: totalTax,
            IsTaxExempt: prop.IsTaxExempt,
            Status: prop.Status,
            IssuedAtUtc: issuedAtUtc);
    }

    public async Task RecordPrintAuditAsync(
        Guid tenantId,
        string action,
        string entityId,
        string detailsJson,
        CancellationToken cancellationToken)
    {
        await dbContext.AuditLogs.AddAsync(new AuditLog
        {
            TenantId = tenantId,
            EntityName = nameof(Property),
            EntityId = entityId,
            Action = action,
            ActorId = currentUserService.UserId?.ToString(),
            ActorRole = currentUserService.Role?.ToString(),
            CorrelationId = currentUserService.CorrelationId,
            IpAddress = currentUserService.IpAddress,
            UserAgent = currentUserService.UserAgent,
            NewValuesJson = detailsJson,
            TimestampUtc = DateTimeOffset.UtcNow,
        }, cancellationToken).ConfigureAwait(false);

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<PrintContextData> LoadTenantPrintContextAsync(
        Guid tenantId,
        Guid? propertyId,
        string? wardNo,
        Guid? assessmentYearId,
        IReadOnlyCollection<Guid>? propertyIds,
        bool loadProtectedImages,
        CancellationToken cancellationToken)
    {
        Tenant tenant = await dbContext.Tenants
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == tenantId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Tenant {tenantId} was not found.");

        GramPanchayat? gp = await dbContext.GramPanchayats
            .AsNoTracking()
            .FirstOrDefaultAsync(g => g.TenantId == tenantId, cancellationToken)
            .ConfigureAwait(false);

        AssessmentYear? year = assessmentYearId.HasValue && assessmentYearId.Value != Guid.Empty
            ? await dbContext.AssessmentYears.AsNoTracking().FirstOrDefaultAsync(y => y.Id == assessmentYearId.Value, cancellationToken).ConfigureAwait(false)
            : await dbContext.AssessmentYears.AsNoTracking().OrderByDescending(y => y.IsActive).ThenByDescending(y => y.StartYear).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);

        FormulaSetting? setting = await dbContext.FormulaSettings
            .AsNoTracking()
            .OrderByDescending(f => f.IsActive)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        IQueryable<Property> propQuery = dbContext.Properties.AsNoTracking();
        if (propertyId.HasValue)
        {
            propQuery = propQuery.Where(p => p.Id == propertyId.Value);
        }
        else if (propertyIds is { Count: > 0 })
        {
            propQuery = propQuery.Where(p => propertyIds.Contains(p.Id));
        }

        if (!string.IsNullOrWhiteSpace(wardNo))
        {
            string normalizedWard = wardNo.Trim();
            propQuery = propQuery.Where(p => p.WardNo == normalizedWard);
        }

        List<Property> props = await propQuery
            .OrderBy(p => p.WardNo)
            .ThenBy(p => p.PropertyNo)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (props.Count == 0)
        {
            return PrintContextData.Empty;
        }

        List<Guid> pids = [.. props.Select(p => p.Id)];

        List<OwnerJoinRow> ownersRaw = await (
            from po in dbContext.PropertyOwners.AsNoTracking()
            join c in dbContext.Citizens.AsNoTracking() on po.CitizenId equals c.Id
            where po.IsActive && pids.Contains(po.PropertyId)
            select new OwnerJoinRow(
                po.PropertyId,
                c.FullNameMr,
                c.FullNameEn,
                c.MobileNo,
                c.AadhaarLast4,
                po.IsPrimaryOwner,
                po.OwnershipSharePct))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        Dictionary<Guid, List<OwnerJoinRow>> ownersByProp = ownersRaw
            .GroupBy(o => o.PropertyId)
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(x => x.IsPrimaryOwner).ThenByDescending(x => x.OwnershipSharePct).ToList());

        List<PropertyConstruction> floorsRaw = await dbContext.PropertyConstructions
            .AsNoTracking()
            .Where(f => f.IsActive && pids.Contains(f.PropertyId))
            .OrderBy(f => f.FloorNo)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        Dictionary<Guid, List<PropertyConstruction>> floorsByProp = floorsRaw
            .GroupBy(f => f.PropertyId)
            .ToDictionary(g => g.Key, g => g.ToList());

        Dictionary<Guid, PropertyTaxCalc> taxCalcByProp = year is null
            ? []
            : await dbContext.PropertyTaxCalcs
                .AsNoTracking()
                .Where(t => t.AssessmentYearId == year.Id && pids.Contains(t.PropertyId))
                .ToDictionaryAsync(t => t.PropertyId, cancellationToken)
                .ConfigureAwait(false);

        Dictionary<string, ConstructionTypeMaster> constructions = await dbContext.ConstructionTypeMasters
            .AsNoTracking()
            .ToDictionaryAsync(c => c.Code, StringComparer.OrdinalIgnoreCase, cancellationToken)
            .ConfigureAwait(false);

        Dictionary<string, UsageWeightage> usages = await dbContext.UsageWeightages
            .AsNoTracking()
            .ToDictionaryAsync(u => u.Code, StringComparer.OrdinalIgnoreCase, cancellationToken)
            .ConfigureAwait(false);

        List<BankAccount> banks = await dbContext.BankAccounts
            .AsNoTracking()
            .Where(b => b.IsActive && !string.IsNullOrEmpty(b.UpiId))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        BankAccount? preferredBank = banks.FirstOrDefault(b => b.IsDefault) ?? banks.FirstOrDefault();
        string? upiId = preferredBank?.UpiId ?? gp?.UpiId;
        string? payeeName = preferredBank?.UpiPayeeName ?? gp?.UpiPayeeName ?? gp?.PanchayatNameEn ?? tenant.NameEn;

        byte[]? gramsevakSig = null;
        byte[]? sarpanchSig = null;
        byte[]? operatorSig = null;
        byte[]? officialStamp = null;

        if (loadProtectedImages && gp is not null)
        {
            gramsevakSig = await TryLoadProtectedImageAsync(tenantId, gp.GramsevakSignatureKey, cancellationToken).ConfigureAwait(false);
            sarpanchSig = await TryLoadProtectedImageAsync(tenantId, gp.SarpanchSignatureKey, cancellationToken).ConfigureAwait(false);
            operatorSig = await TryLoadProtectedImageAsync(tenantId, gp.OperatorSignatureKey, cancellationToken).ConfigureAwait(false);
            officialStamp = await TryLoadProtectedImageAsync(tenantId, gp.OfficialStampKey, cancellationToken).ConfigureAwait(false);
        }

        string yearLabel = year?.YearLabel ?? $"{DateTime.UtcNow.Year}-{(DateTime.UtcNow.Year + 1) % 100:00}";
        string ratePeriod = setting?.RatePeriodName ?? yearLabel;

        return new PrintContextData(
            Properties: props,
            OwnersByProp: ownersByProp,
            FloorsByProp: floorsByProp,
            TaxCalcByProp: taxCalcByProp,
            Constructions: constructions,
            Usages: usages,
            YearId: year?.Id ?? Guid.Empty,
            YearLabel: yearLabel,
            RatePeriodName: ratePeriod,
            LgdCode: gp?.LgdCode ?? tenant.LgdCode,
            PanchayatNameEn: gp?.PanchayatNameEn ?? tenant.NameEn,
            PanchayatNameMr: gp?.PanchayatNameMr ?? tenant.NameMr,
            TehsilName: gp?.TehsilName ?? "-",
            DistrictName: gp?.DistrictName ?? "-",
            Pincode: gp?.Pincode,
            GramsevakName: gp?.GramsevakName,
            SarpanchName: gp?.SarpanchName,
            OperatorName: gp?.OperatorName,
            UpiId: upiId,
            PayeeName: payeeName,
            GramsevakSigBytes: gramsevakSig,
            SarpanchSigBytes: sarpanchSig,
            OperatorSigBytes: operatorSig,
            OfficialStampBytes: officialStamp);
    }

    private async Task<byte[]?> TryLoadProtectedImageAsync(
        Guid tenantId,
        string? assetKey,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(assetKey))
        {
            return null;
        }

        ProtectedAsset? asset = await dbContext.ProtectedAssets
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.AssetKey == assetKey.Trim() && a.TenantId == tenantId, cancellationToken)
            .ConfigureAwait(false);

        if (asset is null)
        {
            return null;
        }

        try
        {
            return piiProtectionService.DecryptBytes(asset.EncryptedContent);
        }
        catch (CryptographicException)
        {
            return null;
        }
    }

    private static SnapshotMetrics ExtractSnapshotMetrics(
        Property p,
        List<PropertyConstruction> floors,
        PropertyTaxCalc? taxCalc,
        Dictionary<string, ConstructionTypeMaster> constructions,
        Dictionary<string, UsageWeightage> usages)
    {
        decimal openLandRrRate = constructions.TryGetValue("OPEN_PLOT", out ConstructionTypeMaster? op)
            ? op.LandAnnualValueRate
            : 0m;
        decimal buildingLandRate = 0m;
        decimal constructionRrRate = 0m;
        decimal capitalValue = 0m;
        string depDisplay = "-";
        string usageDisplay = "-";
        string taxRateDisplay = "-";

        if (floors.Count > 0)
        {
            PropertyConstruction primaryFloor = floors[0];
            if (constructions.TryGetValue(primaryFloor.ConstructionTypeCode, out ConstructionTypeMaster? ct))
            {
                buildingLandRate = ct.LandAnnualValueRate;
                constructionRrRate = ct.ConstructionReadyReckonerRate;
                taxRateDisplay = $"{ct.TaxRate:0.00}";
            }

            if (usages.TryGetValue(primaryFloor.UsageCode, out UsageWeightage? uw))
            {
                usageDisplay = $"{uw.Weightage:0.00}";
            }
        }
        else if (p.OpenPlotAreaSqFt > 0m && op is not null)
        {
            taxRateDisplay = $"{op.TaxRate:0.00}";
            usageDisplay = "1.00";
        }

        if (taxCalc is not null && !string.IsNullOrWhiteSpace(taxCalc.CalculationSnapshotJson))
        {
            try
            {
                CalculatedPropertyTaxDto? snapshot = JsonSerializer.Deserialize<CalculatedPropertyTaxDto>(
                    taxCalc.CalculationSnapshotJson,
                    JsonWebOptions);

                if (snapshot is not null)
                {
                    if (snapshot.Calculation.FloorBreakdowns.Count > 0)
                    {
                        capitalValue = snapshot.Calculation.FloorBreakdowns.Sum(f => f.FloorCapitalValue);
                        depDisplay = string.Join("/", snapshot.Calculation.FloorBreakdowns
                            .Select(f => $"{f.DepreciationPercentage:0}%")
                            .Distinct());
                    }

                    if (p.OpenPlotAreaSqFt > 0m)
                    {
                        decimal openSqM = Math.Round(p.OpenPlotAreaSqFt * CapitalValueTaxCalculator.SqFtToSqM, 2, MidpointRounding.AwayFromZero);
                        capitalValue += Math.Round(openSqM * openLandRrRate, 2, MidpointRounding.AwayFromZero);
                    }
                }
            }
            catch (JsonException)
            {
                // Ignore malformed historical JSON gracefully
            }
        }

        if (taxCalc is null)
        {
            return new SnapshotMetrics(
                OpenLandRrRate: openLandRrRate,
                BuildingLandRate: buildingLandRate,
                ConstructionRrRate: constructionRrRate,
                DepreciationDisplay: depDisplay,
                UsageWeightageDisplay: usageDisplay,
                CapitalValue: capitalValue,
                TaxRateDisplay: taxRateDisplay,
                BuildingTaxComponent: 0m,
                LightingTax: 0m,
                HealthTax: 0m,
                WaterTax: 0m,
                SanitationTax: 0m,
                ToiletTax: 0m,
                OtherTax: p.OtherTax,
                TotalAnnualTax: 0m);
        }

        return new SnapshotMetrics(
            OpenLandRrRate: openLandRrRate,
            BuildingLandRate: buildingLandRate,
            ConstructionRrRate: constructionRrRate,
            DepreciationDisplay: depDisplay,
            UsageWeightageDisplay: usageDisplay,
            CapitalValue: capitalValue,
            TaxRateDisplay: taxRateDisplay,
            BuildingTaxComponent: taxCalc.BuildingTax + taxCalc.OpenLandTax,
            LightingTax: taxCalc.LightingTax,
            HealthTax: taxCalc.HealthTax,
            WaterTax: taxCalc.WaterTax,
            SanitationTax: taxCalc.SanitationTax,
            ToiletTax: taxCalc.ToiletTax,
            OtherTax: taxCalc.OtherTax,
            TotalAnnualTax: taxCalc.TotalTax);
    }

    private static string? BuildUpiPayUri(string? upiId, string? payeeName, decimal amount, string refNo)
    {
        if (string.IsNullOrWhiteSpace(upiId))
        {
            return null;
        }

        string safePa = Uri.EscapeDataString(upiId.Trim());
        string safePn = Uri.EscapeDataString(string.IsNullOrWhiteSpace(payeeName) ? "Gram Panchayat" : payeeName.Trim());
        string safeTn = Uri.EscapeDataString($"Prop {refNo}");
        string amPart = amount > 0m ? $"&am={Math.Round(amount, 0, MidpointRounding.AwayFromZero):0}" : string.Empty;
        return $"upi://pay?pa={safePa}&pn={safePn}{amPart}&tn={safeTn}&cu=INR";
    }

    private static string FormatWaterLabelMr(string code) => code.ToUpperInvariant() switch
    {
        "GENERAL" => "सामान्य नळ (GENERAL)",
        "SPECIAL" => "विशेष नळ (SPECIAL)",
        _ => "नाही (NONE)",
    };

    private sealed record OwnerJoinRow(
        Guid PropertyId,
        string FullNameMr,
        string FullNameEn,
        string? MobileNo,
        string? AadhaarLast4,
        bool IsPrimaryOwner,
        decimal OwnershipSharePct);

    private sealed record SnapshotMetrics(
        decimal OpenLandRrRate,
        decimal BuildingLandRate,
        decimal ConstructionRrRate,
        string DepreciationDisplay,
        string UsageWeightageDisplay,
        decimal CapitalValue,
        string TaxRateDisplay,
        decimal BuildingTaxComponent,
        decimal LightingTax,
        decimal HealthTax,
        decimal WaterTax,
        decimal SanitationTax,
        decimal ToiletTax,
        decimal OtherTax,
        decimal TotalAnnualTax);

    private sealed record PrintContextData(
        List<Property> Properties,
        Dictionary<Guid, List<OwnerJoinRow>> OwnersByProp,
        Dictionary<Guid, List<PropertyConstruction>> FloorsByProp,
        Dictionary<Guid, PropertyTaxCalc> TaxCalcByProp,
        Dictionary<string, ConstructionTypeMaster> Constructions,
        Dictionary<string, UsageWeightage> Usages,
        Guid YearId,
        string YearLabel,
        string RatePeriodName,
        string LgdCode,
        string PanchayatNameEn,
        string PanchayatNameMr,
        string TehsilName,
        string DistrictName,
        string? Pincode,
        string? GramsevakName,
        string? SarpanchName,
        string? OperatorName,
        string? UpiId,
        string? PayeeName,
        byte[]? GramsevakSigBytes,
        byte[]? SarpanchSigBytes,
        byte[]? OperatorSigBytes,
        byte[]? OfficialStampBytes)
    {
        public static PrintContextData Empty { get; } = new(
            [], [], [], [], [], [],
            Guid.Empty, "", "", "", "", "", "", "", null,
            null, null, null, null, null, null, null, null, null);
    }
}
