# Maharashtra Gram Panchayat Directory — Public REST API

A ultra-lightweight, zero-dependency public REST API built in PHP for fetching Maharashtra Gram Panchayat directory data. 

Engineered specifically for **instant village onboarding**, providing:
1. **Instant LGD Code Autofill**: Look up GP Name (English & Marathi), District, Tehsil, and Pincode by entering an LGD code.
2. **Cascading Dropdowns**: Dynamic District ➔ Tehsil / Taluka ➔ Gram Panchayat selectors.
3. **Bilingual Search**: Instant autocomplete search by English name, Marathi name, LGD code, or Pincode.

---

## 📊 Dataset Specifications

- **Total Gram Panchayats:** 27,931
- **Rural Districts:** 34 *(All Maharashtra rural districts; Mumbai City & Suburban excluded as they have 0 Gram Panchayats)*
- **Total Tehsils / Talukas:** 351
- **Database Engine:** SQLite 3 (Indexed B-Tree, 4.41 MB) & MySQL 8 / MariaDB dump (2.62 MB)
- **Response Latency:** `< 1 ms` per query

### Data Schema
| Column | Type | Description | Example |
| :--- | :--- | :--- | :--- |
| `lgd_code` | `VARCHAR(20)` | Unique Local Body Government Code (Primary Key) | `171391` |
| `gp_name_en` | `VARCHAR(150)` | Gram Panchayat Name in English | `Sonimoha` |
| `gp_name_mr` | `VARCHAR(200)` | Gram Panchayat Name in Marathi | `सोनीमोहा` |
| `district` | `VARCHAR(100)` | District Name (Clean) | `Beed` |
| `district_code` | `VARCHAR(20)` | District LGD Code | `428` |
| `tehsil` | `VARCHAR(100)` | Tehsil / Taluka Name (Clean) | `Dharur` |
| `tehsil_code` | `VARCHAR(20)` | Block / Tehsil LGD Code | `4682` |
| `pincode` | `VARCHAR(10)` | Indian Postal PIN Code | `431124` |

---

## 🚀 API Endpoints Reference

All endpoints return JSON with `Access-Control-Allow-Origin: *` (CORS enabled for frontend web & mobile apps) and `Cache-Control` headers for maximum browser and CDN caching.

### 1. Autofill by LGD Code
Fetch full village details instantly when a user enters an LGD code.
- **Method:** `GET`
- **Path:** `/api/v1/panchayat/{lgd_code}` or `/api/v1/panchayat?lgd_code={lgd_code}`
- **Fallback:** `index.php?action=panchayat&lgd_code={lgd_code}`
- **Example Response:**
```json
{
  "success": true,
  "execution_time_ms": 0.66,
  "data": {
    "lgd_code": "171391",
    "gp_name_en": "Sonimoha",
    "gp_name_mr": "सोनीमोहा",
    "district": "Beed",
    "district_code": "428",
    "tehsil": "Dharur",
    "tehsil_code": "4682",
    "pincode": "431124"
  }
}
```

---

### 2. List All Districts
Fetch all 34 rural districts in Maharashtra for the first dropdown.
- **Method:** `GET`
- **Path:** `/api/v1/districts`
- **Fallback:** `index.php?action=districts`
- **Example Response:**
```json
{
  "success": true,
  "execution_time_ms": 7.9,
  "data": {
    "count": 34,
    "districts": [
      { "district": "Ahilyanagar", "district_code": "424", "gp_count": 1323 },
      { "district": "Akola", "district_code": "425", "gp_count": 534 },
      { "district": "Amravati", "district_code": "426", "gp_count": 841 },
      { "district": "Beed", "district_code": "428", "gp_count": 1030 }
    ]
  }
}
```

---

### 3. List Tehsils by District
Fetch all tehsils in the selected district for the second dropdown.
- **Method:** `GET`
- **Path:** `/api/v1/tehsils?district={district_name}`
- **Fallback:** `index.php?action=tehsils&district={district_name}`
- **Example:** `/api/v1/tehsils?district=Beed`
- **Example Response:**
```json
{
  "success": true,
  "execution_time_ms": 0.52,
  "data": {
    "district": "Beed",
    "count": 11,
    "tehsils": [
      { "tehsil": "Ambajogai", "tehsil_code": "4679", "gp_count": 106 },
      { "tehsil": "Ashti", "tehsil_code": "4680", "gp_count": 125 },
      { "tehsil": "Beed", "tehsil_code": "4681", "gp_count": 135 },
      { "tehsil": "Dharur", "tehsil_code": "4682", "gp_count": 56 }
    ]
  }
}
```

---

### 4. List Gram Panchayats by Tehsil
Fetch all Gram Panchayats in the selected tehsil for the third dropdown.
- **Method:** `GET`
- **Path:** `/api/v1/panchayats?district={district}&tehsil={tehsil}`
- **Fallback:** `index.php?action=panchayats&district={district}&tehsil={tehsil}`
- **Example:** `/api/v1/panchayats?district=Beed&tehsil=Dharur`
- **Example Response:**
```json
{
  "success": true,
  "execution_time_ms": 0.44,
  "data": {
    "district": "Beed",
    "tehsil": "Dharur",
    "count": 56,
    "panchayats": [
      {
        "lgd_code": "171363",
        "gp_name_en": "Aaranwadi",
        "gp_name_mr": "आरणवाडी",
        "district": "Beed",
        "tehsil": "Dharur",
        "pincode": "431124"
      },
      {
        "lgd_code": "171391",
        "gp_name_en": "Sonimoha",
        "gp_name_mr": "सोनीमोहा",
        "district": "Beed",
        "tehsil": "Dharur",
        "pincode": "431124"
      }
    ]
  }
}
```

---

### 5. Search / Autocomplete
Search across LGD code, GP name (English or Marathi), or 6-digit Pincode.
- **Method:** `GET`
- **Path:** `/api/v1/search?q={query}&limit=25`
- **Fallback:** `index.php?action=search&q={query}&limit=25`
- **Examples:**
  - By Pincode: `/api/v1/search?q=431124`
  - By English Name: `/api/v1/search?q=Sonimoha`
  - By Marathi Name: `/api/v1/search?q=सोनीमोहा`
- **Example Response:**
```json
{
  "success": true,
  "execution_time_ms": 0.67,
  "data": {
    "query": "431124",
    "count": 2,
    "results": [
      {
        "lgd_code": "171391",
        "gp_name_en": "Sonimoha",
        "gp_name_mr": "सोनीमोहा",
        "district": "Beed",
        "tehsil": "Dharur",
        "pincode": "431124"
      }
    ]
  }
}
```

---

### 6. Directory Stats
Get overall directory summary and health.
- **Method:** `GET`
- **Path:** `/api/v1/stats`
- **Response:**
```json
{
  "success": true,
  "execution_time_ms": 8.28,
  "data": {
    "state": "Maharashtra",
    "state_code": 27,
    "total_districts": 34,
    "total_tehsils": 351,
    "total_panchayats": 27931,
    "api_version": "1.0.0"
  }
}
```

---

## 📁 Project Structure

```
external-api-proj/
├── data/
│   ├── panchayat_directory.db    # Fast indexed SQLite database (4.4 MB)
│   ├── panchayat_directory.sql   # Ready-to-import MySQL dump (2.6 MB)
│   └── .htaccess                 # Protects database files from web downloads
├── public/
│   ├── index.php                 # Single REST router and API controller
│   └── .htaccess                 # Apache / LiteSpeed URL rewrite rules & compression
├── src/
│   ├── Config.php                # Database path and CORS settings
│   ├── Database.php              # Optimized SQLite PDO connection singleton
│   └── DirectoryService.php      # SQL queries and business logic
├── scripts/
│   ├── extract_data.py           # Extractor from source meripanchayat_data.db
│   └── export_mysql.py           # MySQL SQL dump exporter
├── tests/
│   └── test_api.sh               # 14 automated unit and integration tests
├── .htaccess                     # Root rewrite fallback for shared hosting
├── index.php                     # Root entrypoint fallback
└── README.md                     # Documentation
```

---

## 🌐 Deployment to Shared Hosting (cPanel / Hostinger)

### Method 1: SQLite Deployment (Recommended — 2-minute zero setup!)
1. Compress `external-api-proj` folder into `api.zip` (or upload files directly).
2. On your shared hosting (Hostinger, cPanel, Namecheap, etc.):
   - If hosting on a subdomain (e.g. `api.grampanchayat.in`):
     Set the Document Root of your subdomain to the `public/` directory (or extract to the subdomain folder).
   - If hosting in a subfolder (e.g. `mysite.com/gp-api/`):
     Simply extract all files into `/public_html/gp-api/`. The root `.htaccess` and `index.php` will handle routing automatically.
3. Verify permissions:
   - Ensure `data/panchayat_directory.db` has read permissions (`644`).
4. That's it! PHP SQLite PDO is enabled by default on 99.9% of shared hosting providers.

### Method 2: MySQL / phpMyAdmin Deployment (Optional)
If your host prefers MySQL:
1. Open **phpMyAdmin** in your cPanel.
2. Create a database named `gpmh_directory`.
3. Click **Import** and select `data/panchayat_directory.sql`.
4. Update `src/Database.php` with your MySQL credentials.

---

## 🧪 Local Testing

To run the automated test suite locally:
```bash
cd external-api-proj
./tests/test_api.sh
```

To run a local development server:
```bash
php -S 127.0.0.1:8080 -t public/
```
Then visit `http://127.0.0.1:8080/` in your browser to view the interactive API reference.

