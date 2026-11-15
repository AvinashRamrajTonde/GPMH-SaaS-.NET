<?php
declare(strict_types=1);

namespace GpmhDirectory;

use PDO;
use PDOException;
use RuntimeException;

class Database
{
    private static ?PDO $instance = null;

    public static function getConnection(): PDO
    {
        if (self::$instance !== null) {
            return self::$instance;
        }

        $dbPath = Config::getDatabasePath();

        if (!file_exists($dbPath)) {
            throw new RuntimeException("Database file not found at: {$dbPath}");
        }

        try {
            $pdo = new PDO("sqlite:{$dbPath}", null, null, [
                PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION,
                PDO::ATTR_DEFAULT_FETCH_MODE => PDO::FETCH_ASSOC,
                PDO::ATTR_EMULATE_PREPARES => false,
            ]);

            // Performance and Read-Only optimizations
            $pdo->exec("PRAGMA query_only = ON;");
            $pdo->exec("PRAGMA temp_store = MEMORY;");
            $pdo->exec("PRAGMA cache_size = -32000;"); // 32MB in-memory cache
            $pdo->exec("PRAGMA mmap_size = 67108864;"); // 64MB memory-mapped I/O

            // Register Unicode-aware case-folding function for UTF-8/Marathi queries
            if ($pdo->getAttribute(PDO::ATTR_DRIVER_NAME) === 'sqlite') {
                $pdo->sqliteCreateFunction(
                    'unicode_lower',
                    static fn(?string $val): string => $val === null ? '' : (function_exists('mb_strtolower') ? mb_strtolower($val, 'UTF-8') : strtolower($val)),
                    1,
                    PDO::SQLITE_DETERMINISTIC
                );
            }

            self::$instance = $pdo;
            return self::$instance;
        } catch (PDOException $e) {
            throw new RuntimeException("Database connection error: " . $e->getMessage(), (int) $e->getCode(), $e);
        }
    }
}

