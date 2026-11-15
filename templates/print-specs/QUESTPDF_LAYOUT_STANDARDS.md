# QuestPDF Statutory Layout Standards (`templates/print-specs/`)

| Statutory Form | Paper Size & Orientation | Structural Layout & Mandatory Elements | Reference Prototype |
| :--- | :--- | :--- | :--- |
| **Namuna 8 Extract** | **A4 Landscape** (`297×210mm`) | Full **29 columns**, bilingual Devanagari/English headers, Chaturseema (`पूर्व/पश्चिम/उत्तर/दक्षिण`), QR verification link, tripartite signatures | `08_namuna_8_statutory_print_extract.html` |
| **Namuna 9 Register** | **A4 Landscape** 2-Page Spread | Left spread Demand + Right spread Recovery (`Formats A General, B Water, C Consolidated`) | `namuna9_register_print.html` |
| **Namuna 9-Ka Demand Bill** | **A5** or **2-in-1 A4 Landscape** | Top/Bottom or Left/Right split with `✂️` perforation, dynamic NPCI `upi://pay?pa=...` QR, **15-day** recovery warning | `13_namuna_9ka_bill_print_template.html` |
| **Namuna 10 Tax Receipt** | **A4 Landscape Dual Copy** | Left **138mm** Office Copy (`दप्तर प्रत`) + **8mm** Perforation `✂️` + Right **138mm** Payer Copy (`करदाता प्रत`), **0% overflow** | `17_namuna_10_dual_receipt_print.html` |
| **Namuna 11 Misc Receipt** | **A4 Landscape Dual Copy** | Identical 138/8/138mm dual copy geometry for shop rent, certificate fees, NOCs, market fees | `20_misc_receipt_dual_print.html` |
| **Smart Property QR Plate** | **18"×9" Single** & **9-in-1 A4 Sheet** | Bilingual GP emblem, House/Property No, Owner Name, QR link to public `/verify/extract/{token}` | `19_digital_nameplate_qr_print.html` |
| **Namuna 8 Mutation Register** | **A4 Landscape** | Formal monthly resolution number, 15-day public hearing confirmation, old vs new owner columns | `namuna8_mutation_register_print.html` |

*All QuestPDF documents MUST bundle `NotoSansDevanagari-Regular.ttf` and `NotoSansDevanagari-Bold.ttf`.*

