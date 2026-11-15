<?php
declare(strict_types=1);

namespace GpmhDirectory;

class Config
{
    public const API_VERSION = '1.0.0';

    /**
     * Absolute or relative path to the SQLite database
     */
    public static function getDatabasePath(): string
    {
        // Allow environment variable override, else default to data/panchayat_directory.db
        $envPath = getenv('GPMH_DB_PATH');
        if ($envPath && file_exists($envPath)) {
            return $envPath;
        }

        return dirname(__DIR__) . '/data/panchayat_directory.db';
    }

    /**
     * CORS Allowed Origins (* for public read-only API)
     */
    public static function getAllowedOrigins(): string
    {
        return '*';
    }

    /**
     * Cache Control Max Age in seconds (86400 = 24 hours)
     */
    public static function getCacheMaxAge(): int
    {
        return 86400;
    }
}

