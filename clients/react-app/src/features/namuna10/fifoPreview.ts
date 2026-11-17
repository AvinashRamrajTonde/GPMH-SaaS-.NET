import type { Namuna10RegisterRowDto, TaxHeadDuesDto } from '../../services/namuna10Api';

export interface FifoPreviewHeadRow {
  key:
    | 'fees'
    | 'house'
    | 'lighting'
    | 'health'
    | 'sanitation'
    | 'toilet'
    | 'generalWater'
    | 'specialWater'
    | 'other';
  arrearsDue: number;
  arrearsPaid: number;
  currentDue: number;
  currentPaid: number;
  totalPaid: number;
}

export interface FifoLivePreviewResult {
  noticePaid: number;
  warrantPaid: number;
  carriedFeesPaid: number;
  penaltyPaid: number;
  arrearsPaidTotal: number;
  currentPaidTotal: number;
  appliedTowardDemand: number;
  excessCredit: number;
  rows: FifoPreviewHeadRow[];
}

function toPaise(val: number): number {
  if (!Number.isFinite(val) || val <= 0) return 0;
  return Math.round(val * 100);
}

function fromPaise(p: number): number {
  return Math.round(p) / 100;
}

function consumePaise(state: { rem: number }, duePaise: number): number {
  if (state.rem <= 0 || duePaise <= 0) return 0;
  const take = Math.min(state.rem, duePaise);
  state.rem -= take;
  return take;
}

function allocateBucketPaise(state: { rem: number }, due: TaxHeadDuesDto) {
  const house = consumePaise(state, toPaise(due.houseTax));
  const lighting = consumePaise(state, toPaise(due.lightingTax));
  const health = consumePaise(state, toPaise(due.healthTax));
  const sanitation = consumePaise(state, toPaise(due.sanitationTax));
  const toilet = consumePaise(state, toPaise(due.toiletTax));

  // Water head maps GeneralWater first, then SpecialWater matching Namuna10FifoBridge
  const genWaterDue = toPaise(due.generalWaterTax);
  const specWaterDue = toPaise(due.specialWaterTax);
  const totalWaterAllocated = consumePaise(state, genWaterDue + specWaterDue);
  const generalWater = Math.min(totalWaterAllocated, genWaterDue);
  const specialWater = Math.max(0, totalWaterAllocated - generalWater);

  const other = consumePaise(state, toPaise(due.otherTax));

  return {
    house,
    lighting,
    health,
    sanitation,
    toilet,
    generalWater,
    specialWater,
    other,
    total: house + lighting + health + sanitation + toilet + generalWater + specialWater + other,
  };
}

/**
 * Mirrors backend FifoPaymentAllocator & Namuna10FifoBridge in exact integer paise:
 * Notice -> Warrant -> Carried/Penalty Fees -> Arrears Heads -> Current Heads -> Excess Credit.
 */
export function computeLiveFifoPreview(
  row: Namuna10RegisterRowDto,
  amountPaid: number,
  estimatedEarlyRebate = 0,
): FifoLivePreviewResult {
  const state = { rem: toPaise(amountPaid) };

  const noticePaidP = consumePaise(state, toPaise(row.noticeFeeDue));
  const warrantPaidP = consumePaise(state, toPaise(row.warrantFeeDue));

  const carriedFeesDueP = toPaise(row.carriedFeesDue);
  const currentPenaltyDueP = toPaise(row.penaltyDue);
  const penaltyCombinedPaidP = consumePaise(state, carriedFeesDueP + currentPenaltyDueP);
  const carriedFeesPaidP = Math.min(carriedFeesDueP, penaltyCombinedPaidP);
  const penaltyPaidP = Math.max(0, penaltyCombinedPaidP - carriedFeesPaidP);

  const arrPaid = allocateBucketPaise(state, row.arrearsDue);

  // Reduce eligible current general heads if previewing early rebate discount
  const curAdjusted: TaxHeadDuesDto = { ...row.currentDue };
  if (estimatedEarlyRebate > 0) {
    let rebLeft = toPaise(estimatedEarlyRebate);
    const generalKeys: (keyof Pick<
      TaxHeadDuesDto,
      'houseTax' | 'lightingTax' | 'healthTax' | 'sanitationTax' | 'toiletTax' | 'otherTax'
    >)[] = ['houseTax', 'lightingTax', 'healthTax', 'sanitationTax', 'toiletTax', 'otherTax'];

    for (const k of generalKeys) {
      const dueP = toPaise(curAdjusted[k]);
      const cut = Math.min(rebLeft, dueP);
      curAdjusted[k] = fromPaise(dueP - cut);
      rebLeft -= cut;
    }
  }

  const curPaid = allocateBucketPaise(state, curAdjusted);

  const arrearsFeesDue = row.carriedFeesDue;
  const currentFeesDue = row.noticeFeeDue + row.warrantFeeDue + row.penaltyDue;
  const currentFeesPaidP = noticePaidP + warrantPaidP + penaltyPaidP;

  const rows: FifoPreviewHeadRow[] = [
    {
      key: 'fees',
      arrearsDue: arrearsFeesDue,
      arrearsPaid: fromPaise(carriedFeesPaidP),
      currentDue: currentFeesDue,
      currentPaid: fromPaise(currentFeesPaidP),
      totalPaid: fromPaise(carriedFeesPaidP + currentFeesPaidP),
    },
    {
      key: 'house',
      arrearsDue: row.arrearsDue.houseTax,
      arrearsPaid: fromPaise(arrPaid.house),
      currentDue: curAdjusted.houseTax,
      currentPaid: fromPaise(curPaid.house),
      totalPaid: fromPaise(arrPaid.house + curPaid.house),
    },
    {
      key: 'lighting',
      arrearsDue: row.arrearsDue.lightingTax,
      arrearsPaid: fromPaise(arrPaid.lighting),
      currentDue: curAdjusted.lightingTax,
      currentPaid: fromPaise(curPaid.lighting),
      totalPaid: fromPaise(arrPaid.lighting + curPaid.lighting),
    },
    {
      key: 'health',
      arrearsDue: row.arrearsDue.healthTax,
      arrearsPaid: fromPaise(arrPaid.health),
      currentDue: curAdjusted.healthTax,
      currentPaid: fromPaise(curPaid.health),
      totalPaid: fromPaise(arrPaid.health + curPaid.health),
    },
    {
      key: 'sanitation',
      arrearsDue: row.arrearsDue.sanitationTax,
      arrearsPaid: fromPaise(arrPaid.sanitation),
      currentDue: curAdjusted.sanitationTax,
      currentPaid: fromPaise(curPaid.sanitation),
      totalPaid: fromPaise(arrPaid.sanitation + curPaid.sanitation),
    },
    {
      key: 'toilet',
      arrearsDue: row.arrearsDue.toiletTax,
      arrearsPaid: fromPaise(arrPaid.toilet),
      currentDue: curAdjusted.toiletTax,
      currentPaid: fromPaise(curPaid.toilet),
      totalPaid: fromPaise(arrPaid.toilet + curPaid.toilet),
    },
    {
      key: 'generalWater',
      arrearsDue: row.arrearsDue.generalWaterTax,
      arrearsPaid: fromPaise(arrPaid.generalWater),
      currentDue: curAdjusted.generalWaterTax,
      currentPaid: fromPaise(curPaid.generalWater),
      totalPaid: fromPaise(arrPaid.generalWater + curPaid.generalWater),
    },
    {
      key: 'specialWater',
      arrearsDue: row.arrearsDue.specialWaterTax,
      arrearsPaid: fromPaise(arrPaid.specialWater),
      currentDue: curAdjusted.specialWaterTax,
      currentPaid: fromPaise(curPaid.specialWater),
      totalPaid: fromPaise(arrPaid.specialWater + curPaid.specialWater),
    },
    {
      key: 'other',
      arrearsDue: row.arrearsDue.otherTax,
      arrearsPaid: fromPaise(arrPaid.other),
      currentDue: curAdjusted.otherTax,
      currentPaid: fromPaise(curPaid.other),
      totalPaid: fromPaise(arrPaid.other + curPaid.other),
    },
  ];

  const totalFeesPaidP = noticePaidP + warrantPaidP + carriedFeesPaidP + penaltyPaidP;
  const arrearsPaidTotalP = carriedFeesPaidP + arrPaid.total;
  const currentPaidTotalP = currentFeesPaidP + curPaid.total;

  return {
    noticePaid: fromPaise(noticePaidP),
    warrantPaid: fromPaise(warrantPaidP),
    carriedFeesPaid: fromPaise(carriedFeesPaidP),
    penaltyPaid: fromPaise(penaltyPaidP),
    arrearsPaidTotal: fromPaise(arrearsPaidTotalP),
    currentPaidTotal: fromPaise(currentPaidTotalP),
    appliedTowardDemand: fromPaise(totalFeesPaidP + arrPaid.total + curPaid.total),
    excessCredit: fromPaise(state.rem),
    rows,
  };
}

