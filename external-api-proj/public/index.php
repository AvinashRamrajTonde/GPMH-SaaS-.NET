<?php
declare(strict_types=1);

/**
 * Maharashtra Gram Panchayat Directory - Public REST API
 * Shared Hosting Compatible (No Composer required)
 */

$startTime = microtime(true);

// 1. Autoloader for GpmhDirectory namespace
spl_autoload_register(function ($class) {
    $prefix = 'GpmhDirectory\\';
    $baseDir = dirname(__DIR__) . '/src/';

    $len = strlen($prefix);
    if (strncmp($prefix, $class, $len) !== 0) {
        return;
    }

    $relativeClass = substr($class, $len);
    $file = $baseDir . str_replace('\\', '/', $relativeClass) . '.php';

    if (file_exists($file)) {
        require_once $file;
    }
});

use GpmhDirectory\Config;
use GpmhDirectory\DirectoryService;

// 2. Global CORS and Response Headers
header('Content-Type: application/json; charset=UTF-8');
header('Access-Control-Allow-Origin: ' . Config::getAllowedOrigins());
header('Access-Control-Allow-Methods: GET, OPTIONS');
header('Access-Control-Allow-Headers: Content-Type, Authorization, X-Requested-With');

// Handle preflight OPTIONS request
if ($_SERVER['REQUEST_METHOD'] === 'OPTIONS') {
    http_response_code(204);
    exit;
}

// Ensure GET method only
if ($_SERVER['REQUEST_METHOD'] !== 'GET') {
    http_response_code(405);
    echo json_encode([
        'success' => false,
        'error' => [
            'code' => 'METHOD_NOT_ALLOWED',
            'message' => 'Only GET requests are supported.'
        ]
    ], JSON_UNESCAPED_UNICODE);
    exit;
}

// 3. Response Helper Functions
function sendResponse(array $data, int $statusCode = 200, bool $cacheable = true): void
{
    global $startTime;
    $durationMs = round((microtime(true) - $startTime) * 1000, 2);
    header("X-Response-Time-Ms: {$durationMs}");

    if ($cacheable) {
        header('Cache-Control: public, max-age=' . Config::getCacheMaxAge());
    } else {
        header('Cache-Control: no-cache, no-store, must-revalidate');
    }

    http_response_code($statusCode);
    echo json_encode([
        'success' => true,
        'execution_time_ms' => $durationMs,
        'data' => $data
    ], JSON_UNESCAPED_UNICODE | JSON_PRETTY_PRINT);
    exit;
}

function sendError(string $message, string $code = 'BAD_REQUEST', int $statusCode = 400): void
{
    global $startTime;
    $durationMs = round((microtime(true) - $startTime) * 1000, 2);
    header("X-Response-Time-Ms: {$durationMs}");
    header('Cache-Control: no-cache, no-store, must-revalidate');

    http_response_code($statusCode);
    echo json_encode([
        'success' => false,
        'execution_time_ms' => $durationMs,
        'error' => [
            'code' => $code,
            'message' => $message
        ]
    ], JSON_UNESCAPED_UNICODE | JSON_PRETTY_PRINT);
    exit;
}

// 4. Request URI and Routing Parser
try {
    $service = new DirectoryService();
} catch (\Throwable $e) {
    error_log('[LGD-API] Initialization error: ' . $e->getMessage());
    sendError('Directory service is temporarily unavailable.', 'SERVICE_UNAVAILABLE', 503);
}

// Parse request path
$requestUri = parse_url($_SERVER['REQUEST_URI'] ?? '/', PHP_URL_PATH) ?? '/';
$scriptName = $_SERVER['SCRIPT_NAME'] ?? '';
$scriptDir = dirname($scriptName);

// Strip script directory or index.php from path
if ($scriptDir !== '/' && strpos($requestUri, $scriptDir) === 0) {
    $requestUri = substr($requestUri, strlen($scriptDir));
}
if (strpos($requestUri, '/index.php') === 0) {
    $requestUri = substr($requestUri, strlen('/index.php'));
}
$path = trim($requestUri, '/');

// Query parameter fallback support for hosts without mod_rewrite
$action = $_GET['action'] ?? null;

// Normalize path or fallback
if ($action !== null) {
    $segments = explode('/', trim($action, '/'));
} else {
    $segments = $path === '' ? [] : explode('/', $path);
}

// Remove leading "api" or "v1" if present
if (isset($segments[0]) && $segments[0] === 'api') {
    array_shift($segments);
}
if (isset($segments[0]) && $segments[0] === 'v1') {
    array_shift($segments);
}

$resource = $segments[0] ?? '';
$param = $segments[1] ?? null;

// 5. Route Handling
switch ($resource) {
    case '':
    case 'health':
        // Welcome and Health Info
        sendResponse([
            'status' => 'online',
            'service' => 'Maharashtra Gram Panchayat Directory API',
            'version' => Config::API_VERSION,
            'documentation' => [
                'endpoints' => [
                    'GET /api/v1/districts' => 'List all 34 rural districts in Maharashtra',
                    'GET /api/v1/tehsils?district={name}' => 'List all tehsils/talukas in a district',
                    'GET /api/v1/panchayats?district={name}&tehsil={name}' => 'List all Gram Panchayats in a tehsil',
                    'GET /api/v1/panchayat/{lgd_code}' => 'Get Gram Panchayat details by LGD Code (or ?lgd_code=...)',
                    'GET /api/v1/search?q={query}&limit=25' => 'Search by LGD code, GP name (EN/MR), or Pincode',
                    'GET /api/v1/stats' => 'Get directory statistics'
                ],
                'shared_hosting_query_params' => [
                    'Example 1' => 'index.php?action=districts',
                    'Example 2' => 'index.php?action=tehsils&district=Beed',
                    'Example 3' => 'index.php?action=panchayats&district=Beed&tehsil=Dharur',
                    'Example 4' => 'index.php?action=panchayat&lgd_code=171391',
                    'Example 5' => 'index.php?action=search&q=Sonimoha'
                ]
            ]
        ]);
        break;

    case 'stats':
        $stats = $service->getStats();
        sendResponse($stats);
        break;

    case 'districts':
        $districts = $service->getDistricts();
        sendResponse([
            'count' => count($districts),
            'districts' => $districts
        ]);
        break;

    case 'tehsils':
        $district = $_GET['district'] ?? $param;
        if (!$district) {
            sendError("Query parameter 'district' is required (e.g., /api/v1/tehsils?district=Beed)", 'MISSING_PARAM', 400);
        }
        $tehsils = $service->getTehsils($district);
        if (empty($tehsils)) {
            sendError("District '{$district}' not found or has no recorded tehsils.", 'NOT_FOUND', 404);
        }
        sendResponse([
            'district' => $district,
            'count' => count($tehsils),
            'tehsils' => $tehsils
        ]);
        break;

    case 'panchayats':
        $district = $_GET['district'] ?? null;
        $tehsil = $_GET['tehsil'] ?? null;

        if (!$district || !$tehsil) {
            sendError("Parameters 'district' and 'tehsil' are both required (e.g., /api/v1/panchayats?district=Beed&tehsil=Dharur)", 'MISSING_PARAM', 400);
        }

        $panchayats = $service->getPanchayats($district, $tehsil);
        sendResponse([
            'district' => $district,
            'tehsil' => $tehsil,
            'count' => count($panchayats),
            'panchayats' => $panchayats
        ]);
        break;

    case 'panchayat':
        $lgdCode = $param ?? ($_GET['lgd_code'] ?? null);
        if (!$lgdCode) {
            sendError("Parameter 'lgd_code' is required (e.g., /api/v1/panchayat/171391 or ?lgd_code=171391)", 'MISSING_PARAM', 400);
        }

        $gp = $service->getPanchayatByLgd($lgdCode);
        if (!$gp) {
            sendError("Gram Panchayat with LGD Code '{$lgdCode}' not found.", 'NOT_FOUND', 404);
        }
        sendResponse($gp);
        break;

    case 'search':
        $q = $_GET['q'] ?? '';
        if (trim($q) === '') {
            sendError("Query parameter 'q' is required (e.g., /api/v1/search?q=Sonimoha or ?q=431124)", 'MISSING_PARAM', 400);
        }
        $limit = isset($_GET['limit']) ? (int) $_GET['limit'] : 25;
        $results = $service->search($q, $limit);
        sendResponse([
            'query' => $q,
            'count' => count($results),
            'results' => $results
        ]);
        break;

    default:
        sendError("Endpoint not found: '/{$path}'. Visit '/' for documentation.", 'ENDPOINT_NOT_FOUND', 404);
        break;
}

