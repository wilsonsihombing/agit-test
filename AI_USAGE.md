# AI Usage & Engineering Provenance

Dokumen ini mendokumentasikan interaksi terpenting antara engineer dan AI assistant selama perancangan dan implementasi MVP Access Request Hub, evaluasi output AI, serta audit terhadap kekeliruan yang ditemukan.

---

## 1. Interaksi AI yang Paling Berpengaruh

### Interaksi 1: Pemilihan dan Pivot Arsitektur Backend (.NET 9 + EF Core)
* **Ask**: Rekomendasi arsitektur untuk MVP Access Request Hub yang mendukung relational database, foreign key constraints, transaksi atomik, dan kontrol konkurensi.
* **Suggestion**: AI awalnya merekomendasikan Node.js/Express + SQLite. Namun ketika requirement diperbarui oleh engineer untuk menggunakan .NET, AI menyarankan ASP.NET Core 9 Web API dengan Entity Framework Core 9 (EF Core) dan SQLite provider.
* **Accepted / Changed / Rejected**: **Accepted with Architectural Adjustments**.
* **Why**: ASP.NET Core 9 memiliki performa enterprise yang tinggi, type safety kelas satu melalui C# 13, integrasi native `[ConcurrencyCheck]` pada EF Core untuk menangani optimistic locking secara otomatis, serta `WebApplicationFactory` untuk integrasi testing headless yang deterministik.

### Interaksi 2: Strategi Optimistic Concurrency Token
* **Ask**: Bagaimana cara terbaik mencegah *race condition* dan benturan approval dari dua approver yang bertindak hampir bersamaan?
* **Suggestion**: AI menyarankan menggunakan HTTP ETag atau timestamp sub-milidetik.
* **Accepted / Changed / Rejected**: **Changed (Disesuaikan)**.
* **Why**: AI diarahkan untuk menggunakan *monotonically increasing integer* `version` (1, 2, 3...) yang dikombinasikan dengan klausa `WHERE Id = @id AND Version = @version` pada EF Core. Integer versioning kebal terhadap masalah *clock skew* antar node server, deterministik, dan nilainya langsung tercatat di dalam tabel `audit_events` sebagai rekam jejak versi record.

### Interaksi 3: Skema Idempotency pada Pembuatan Request
* **Ask**: Bagaimana menjamin *idempotent create* saat client melakukan *retry* atau pengiriman berulang?
* **Suggestion**: Menyimpan `ClientRequestId` di in-memory cache (misal Redis/Dictionary) dengan TTL.
* **Accepted / Changed / Rejected**: **Rejected**.
* **Why**: Menolak saran in-memory cache karena melanggar prinsip ketahanan kegagalan (*durability*). Jika server restart, data idempotency hilang. AI diarahkan untuk menempatkan `ClientRequestId` langsung pada tabel relasional `access_requests` dengan *UNIQUE constraint*. Dengan demikian, database menjamin integritas bahkan saat dua proses concurrent mencoba melakukan submit bersamaan.

### Interaksi 4: Simulasi Autentikasi vs Boundary Otorisasi
* **Ask**: Bagaimana mendesain autentikasi dan otorisasi sesuai batasan Section 2.2?
* **Suggestion**: Membuat mock JWT generator dengan endpoint login.
* **Accepted / Changed / Rejected**: **Changed**.
* **Why**: Mengikuti instruksi Section 2.2 ("Authentication boleh disimulasikan dengan user switcher / request header"), pendekatan disederhanakan menggunakan middleware yang membaca header `x-user-id`. Seluruh kompleksitas dialihkan ke validasi *server-side authorization* di controller dan service (memastikan hanya assigned manager / system owner yang berhak melakukan action, dan melarang self-approval).

---

## 2. Three Things AI Got Wrong (Kekeliruan AI dan Perbaikannya)

### 1. Urutan Evaluasi Status Terminal vs Concurrency Version Check
* **Kekeliruan AI**: Pada implementasi awal `RejectRequestAsync`, AI meletakkan pengecekan status terminal (`if (request.Status == "APPROVED" || request.Status == "REJECTED")`) **sebelum** pengecekan versi konkurensi (`if (request.Version != expectedVersion)`).
* **Dampak**: Ketika dua approver melakukan aksi bersamaan pada versi yang sama (misal Bob approve dan approver lain reject), request kedua menerima error `400 Bad Request` ("Cannot transition terminal state") alih-alih `409 Conflict`.
* **Koreksi Engineer**: Engineer mereview dan memindahkan evaluasi `request.Version != expectedVersion` ke urutan pertama sebelum pemeriksaan status bisnis, sehingga setiap aksi dengan token kadaluarsa secara konsisten menghasilkan HTTP 409 Conflict.

### 2. Penamaan Properti Serialisasi JSON pada Middleware Error Handling
* **Kekeliruan AI**: AI menulis `JsonSerializer.Serialize(response)` pada `ErrorHandlingMiddleware` tanpa konfigurasi `PropertyNamingPolicy`, sehingga output JSON menggunakan PascalCase (`{"Error": {"Code": "FORBIDDEN"}}`).
* **Dampak**: Klien frontend dan automated test suite yang mengacu pada standar camelCase API (`response.error.code`) mengalami kegagalan parsing (*KeyNotFoundException*).
* **Koreksi Engineer**: Menambahkan `JsonSerializerOptions` dengan `PropertyNamingPolicy = JsonNamingPolicy.CamelCase` pada middleware error handling untuk menyelaraskan output response ke seluruh pipeline API.

### 3. Ketidakcocokan Versi Paket NuGet (.NET 9 vs Preview .NET 10)
* **Kekeliruan AI**: Ketika menjalankan perintah `dotnet add package Microsoft.EntityFrameworkCore.Sqlite`, AI tidak menyertakan argumen versi. Akibatnya, NuGet CLI mengunduh paket rilis preview .NET 10.x yang tidak kompatibel dengan SDK .NET 9.0 target proyek (`NU1202: Package is not compatible with net9.0`).
* **Dampak**: Restore paket gagal.
* **Koreksi Engineer**: Mengoreksi perintah dengan menyematkan flag versi eksplisit `--version "9.0.*"` untuk seluruh paket dependensi EF Core dan testing.
