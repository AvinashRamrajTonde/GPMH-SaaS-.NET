#!/usr/bin/env bash
set -e

PORT=8099
HOST="127.0.0.1"
BASE_URL="http://${HOST}:${PORT}"
DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

echo "=========================================================="
echo "🧪 Running Maharashtra GP Directory API Tests"
echo "=========================================================="

# Start PHP built-in web server
echo "Starting local PHP server on ${BASE_URL}..."
php -S "${HOST}:${PORT}" -t "${DIR}/public" > /dev/null 2>&1 &
SERVER_PID=$!

cleanup() {
    echo "Stopping test server (PID: ${SERVER_PID})..."
    kill -9 "${SERVER_PID}" 2>/dev/null || true
}
trap cleanup EXIT

# Wait for server readiness (up to ~5s)
for _ in $(seq 1 25); do
    if curl -s -o /dev/null "${BASE_URL}/"; then
        break
    fi
    sleep 0.2
done

# Test helper
check_endpoint() {
    local desc="$1"
    local url="$2"
    local expected_code="$3"
    local expected_substring="$4"

    echo -n "  Testing: ${desc}... "
    local response
    response=$(curl -s -w "\n%{http_code}" "${url}")
    local http_code
    http_code=$(echo "${response}" | tail -n1)
    local body
    body=$(echo "${response}" | head -n -1)

    if [ "${http_code}" != "${expected_code}" ]; then
        echo "❌ FAILED (HTTP ${http_code}, Expected ${expected_code})"
        echo "Response: ${body}"
        exit 1
    fi

    if [ -n "${expected_substring}" ] && ! echo "${body}" | grep -q "${expected_substring}"; then
        echo "❌ FAILED (Substring '${expected_substring}' not found in body)"
        echo "Response: ${body}"
        exit 1
    fi

    echo "✅ PASSED (HTTP ${http_code})"
}

echo ""
echo "--- Testing Core Endpoints ---"
check_endpoint "1. Root Documentation" "${BASE_URL}/" "200" "Maharashtra Gram Panchayat Directory API"
check_endpoint "2. Directory Stats" "${BASE_URL}/api/v1/stats" "200" "total_panchayats"
check_endpoint "3. All Districts" "${BASE_URL}/api/v1/districts" "200" "Ahilyanagar"
check_endpoint "4. Tehsils for Beed" "${BASE_URL}/api/v1/tehsils?district=Beed" "200" "Dharur"
check_endpoint "5. Panchayats in Dharur" "${BASE_URL}/api/v1/panchayats?district=Beed&tehsil=Dharur" "200" "Sonimoha"
check_endpoint "6. Single GP (REST path)" "${BASE_URL}/api/v1/panchayat/171391" "200" "सोनीमोहा"
check_endpoint "7. Single GP (Query param)" "${BASE_URL}/api/v1/panchayat?lgd_code=171391" "200" "431124"
check_endpoint "8. Search by English Name" "${BASE_URL}/api/v1/search?q=Sonimoha" "200" "171391"
check_endpoint "9. Search by Marathi Name" "${BASE_URL}/api/v1/search?q=%E0%A4%B8%E0%A5%8B%E0%A4%A8%E0%A5%80%E0%A4%AE%E0%A5%8B%E0%A4%B9%E0%A4%BE" "200" "171391"
check_endpoint "10. Search by Pincode" "${BASE_URL}/api/v1/search?q=431124" "200" "Sonimoha"
check_endpoint "11. Shared Hosting Fallback Query Param" "${BASE_URL}/index.php?action=panchayat&lgd_code=171391" "200" "Dharur"

echo ""
echo "--- Testing Error Handling ---"
check_endpoint "12. Not Found LGD Code" "${BASE_URL}/api/v1/panchayat/9999999" "404" "NOT_FOUND"
check_endpoint "13. Missing Tehsil Param" "${BASE_URL}/api/v1/panchayats?district=Beed" "400" "MISSING_PARAM"
check_endpoint "14. Unknown Route" "${BASE_URL}/api/v1/unknown_route" "404" "ENDPOINT_NOT_FOUND"
check_endpoint "15. Escaped Wildcard Search Safety" "${BASE_URL}/api/v1/search?q=%25" "200" '"count": 0'

echo ""
echo "🎉 ALL 15 TESTS PASSED SUCCESSFULLY!"

