<?php
declare(strict_types=1);

namespace GpmhDirectory;

use PDO;

class DirectoryService
{
    private PDO $db;

    public function __construct(?PDO $db = null)
    {
        $this->db = $db ?? Database::getConnection();
    }

    /**
     * Get single Gram Panchayat by LGD Code
     */
    public function getPanchayatByLgd(string $lgdCode): ?array
    {
        $stmt = $this->db->prepare("
            SELECT 
                lgd_code,
                gp_name_en,
                gp_name_mr,
                district,
                district_code,
                tehsil,
                tehsil_code,
                pincode
            FROM panchayats
            WHERE lgd_code = :lgd_code
            LIMIT 1
        ");
        $stmt->execute(['lgd_code' => trim($lgdCode)]);
        $result = $stmt->fetch();

        return $result ?: null;
    }

    /**
     * Get all distinct districts with their codes and GP count
     */
    public function getDistricts(): array
    {
        $stmt = $this->db->query("
            SELECT 
                district,
                MIN(district_code) as district_code,
                COUNT(*) as gp_count
            FROM panchayats
            GROUP BY district
            ORDER BY district ASC
        ");

        return $stmt->fetchAll();
    }

    /**
     * Get all tehsils / talukas in a given district
     */
    public function getTehsils(string $district): array
    {
        $stmt = $this->db->prepare("
            SELECT 
                tehsil,
                MIN(tehsil_code) as tehsil_code,
                COUNT(*) as gp_count
            FROM panchayats
            WHERE LOWER(district) = LOWER(:district)
            GROUP BY tehsil
            ORDER BY tehsil ASC
        ");
        $stmt->execute(['district' => trim($district)]);

        return $stmt->fetchAll();
    }

    /**
     * Get all Gram Panchayats for a specific district and tehsil
     */
    public function getPanchayats(string $district, string $tehsil): array
    {
        $stmt = $this->db->prepare("
            SELECT 
                lgd_code,
                gp_name_en,
                gp_name_mr,
                district,
                tehsil,
                pincode
            FROM panchayats
            WHERE LOWER(district) = LOWER(:district)
              AND LOWER(tehsil) = LOWER(:tehsil)
            ORDER BY gp_name_en ASC
        ");
        $stmt->execute([
            'district' => trim($district),
            'tehsil' => trim($tehsil)
        ]);

        return $stmt->fetchAll();
    }

    /**
     * Autocomplete / Global search by LGD code, GP name (EN/MR), or Pincode
     */
    public function search(string $query, int $limit = 25): array
    {
        $query = trim($query);
        if ($query === '') {
            return [];
        }

        $limit = max(1, min($limit, 100)); // clamp between 1 and 100

        // Exact LGD code match priority
        if (ctype_digit($query) && strlen($query) >= 5) {
            $exactStmt = $this->db->prepare("
                SELECT lgd_code, gp_name_en, gp_name_mr, district, tehsil, pincode
                FROM panchayats
                WHERE lgd_code = :exact_code OR pincode = :exact_pin
                LIMIT :limit
            ");
            $exactStmt->bindValue(':exact_code', $query, PDO::PARAM_STR);
            $exactStmt->bindValue(':exact_pin', $query, PDO::PARAM_STR);
            $exactStmt->bindValue(':limit', $limit, PDO::PARAM_INT);
            $exactStmt->execute();
            $exactResults = $exactStmt->fetchAll();

            if (count($exactResults) >= $limit) {
                return $exactResults;
            }
        }

        $escaped = strtr($query, ['\\' => '\\\\', '%' => '\\%', '_' => '\\_']);
        $pattern = '%' . $escaped . '%';
        $prefix = $escaped . '%';

        $stmt = $this->db->prepare("
            SELECT 
                lgd_code,
                gp_name_en,
                gp_name_mr,
                district,
                tehsil,
                pincode
            FROM panchayats
            WHERE lgd_code LIKE :pattern ESCAPE '\\'
               OR unicode_lower(gp_name_en) LIKE unicode_lower(:pattern) ESCAPE '\\'
               OR unicode_lower(gp_name_mr) LIKE unicode_lower(:pattern) ESCAPE '\\'
               OR pincode LIKE :pattern ESCAPE '\\'
            ORDER BY 
                CASE 
                    WHEN lgd_code = :raw THEN 1
                    WHEN unicode_lower(gp_name_en) LIKE unicode_lower(:prefix) ESCAPE '\\' THEN 2
                    WHEN unicode_lower(gp_name_mr) LIKE unicode_lower(:prefix) ESCAPE '\\' THEN 3
                    ELSE 4
                END,
                gp_name_en ASC
            LIMIT :limit
        ");

        $stmt->bindValue(':pattern', $pattern, PDO::PARAM_STR);
        $stmt->bindValue(':prefix', $prefix, PDO::PARAM_STR);
        $stmt->bindValue(':raw', $query, PDO::PARAM_STR);
        $stmt->bindValue(':limit', $limit, PDO::PARAM_INT);
        $stmt->execute();

        return $stmt->fetchAll();
    }

    /**
     * Get directory statistics
     */
    public function getStats(): array
    {
        $stmt = $this->db->query("
            SELECT 
                COUNT(*) as total_panchayats,
                COUNT(DISTINCT district) as total_districts,
                COUNT(DISTINCT district || '::' || tehsil) as total_tehsils
            FROM panchayats
        ");
        $stats = $stmt->fetch();

        return [
            'state' => 'Maharashtra',
            'state_code' => 27,
            'total_districts' => (int) ($stats['total_districts'] ?? 0),
            'total_tehsils' => (int) ($stats['total_tehsils'] ?? 0),
            'total_panchayats' => (int) ($stats['total_panchayats'] ?? 0),
            'api_version' => Config::API_VERSION,
        ];
    }
}

