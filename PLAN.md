# Implementation Plan: MVP Access Request Hub (Phase 1)

## 1. Problem Understanding & Scope

### 1.1 Background & Objectives
Perusahaan saat ini mengelola permohonan akses aplikasi internal melalui email dan chat. Hal ini menimbulkan berbagai risiko operasional:
- Proses approval tidak terlacak (*untracked status*).
- Terjadi duplikasi pengiriman request (*double submit*).
- Terjadi *race condition* / benturan aksi ketika dua approver mengeksekusi request yang sama pada saat bersamaan.
- Ketiadaan rekam jejak audit (*audit trail*) yang tidak dapat dimanipulasi (*tamper-evident*).

Tujuan Phase 1 adalah membangun **MVP Access Request Hub** sebagai *single source of truth* untuk pembuatan request akses, workflow approval bertingkat, penanganan konkurensi, dan pencatatan audit yang atomik.

### 1.2 Demo Users & Organizational Hierarchy
Sesuai spesifikasi Section 2.2:
- **Alice** (`alice@example.local`): Requester (bawahan Bob).
- **Bob** (`bob@example.local`): Manager dari Alice (memproses Manager approval).
- **Carol** (`carol@example.local`): System Owner aplikasi **CRM**.
- **Dana** (`dana@example.local`): System Owner aplikasi **Finance Portal**.
- **Erin** (`erin@example.local`): Admin / Auditor (memiliki akses melihat seluruh request dan audit trail, bukan approver otomatis).

### 1.3 Master Data & Business Rules
- **Applications**: `CRM` (System Owner: Carol) dan `Finance Portal` (System Owner: Dana).
- **Environments**: `NonProduction` dan `Production`.
- **Access Levels**: `Read` dan `Admin`.
- **Policy Version**: Disimpan sebagai `v1` pada setiap request yang dibuat di Phase 1.
- **High-Risk Rule**: Suatu request dikategorikan **High-risk** jika:
  $$\text{Environment} = \text{'Production'} \quad\lor\quad \text{AccessLevel} = \text{'Admin'}$$
- **Workflow State Transitions**:
  1. **SUBMIT**: Status awal request selalu `PENDING_MANAGER` dengan `current_assignee` diset ke Manager dari requester.
  2. **MANAGER APPROVAL**:
     - Jika **Non-High-Risk**: Langsung beralih ke status terminal `APPROVED` (`current_assignee = NULL`).
     - Jika **High-Risk**: Beralih ke `PENDING_SYSTEM_OWNER` dengan `current_assignee` diset ke System Owner dari aplikasi yang bersangkutan.
  3. **SYSTEM OWNER APPROVAL**:
     - Jika di-approve: Beralih ke status terminal `APPROVED`.
  4. **REJECTION** (Manager atau System Owner):
     - Wajib menyertakan `reason`.
     - Beralih ke status terminal `REJECTED`.
  5. **Terminal Invariants**: Status `APPROVED` dan `REJECTED` tidak dapat diubah lagi. Setiap transisi selanjutnya harus ditolak dengan error 400/409.
  6. **Self-Approval Prevention**: Requester tidak boleh meng-approve request miliknya sendiri.
  7. **Strict Server-Side Authorization**: Hanya user yang sesuai dengan role dan kepemilikan tanggung jawab (Manager dari requester, atau System Owner dari aplikasi) yang boleh melakukan tindakan.
  8. **Idempotency**: Pengiriman request dengan `ClientRequestId` yang sama harus mengembalikan record yang sudah ada (tidak membuat duplikasi data).
  9. **Optimistic Concurrency**: Perubahan status approval/rejection wajib menyertakan versi record (`version`). Jika versi di database sudah berubah, sistem merespons dengan `409 Conflict`.
  10. **Atomic Audit Trail**: Setiap perubahan status wajib mencatat *audit event* secara atomik di dalam database transaction yang sama.

---

## 2. Architecture & Data Model

### 2.1 Technology Stack Selection
- **Runtime & Language**: .NET 9 (C# 13) pada backend untuk enterprise-grade type safety, robust web API, dan high throughput.
- **Backend API**: ASP.NET Core 9 Web API (Controllers, modular middleware, custom exception filter/middleware, server-side authorization).
- **ORM & Database**: Entity Framework Core 9 (EF Core) dengan SQLite provider (`Microsoft.EntityFrameworkCore.Sqlite`), strict foreign keys, check constraints, unique constraints, dan optimistic concurrency token (`[ConcurrencyCheck]` / `IsConcurrencyToken()`).
  - *Alasan*: Zero-configuration, zero-external dependency, deterministik, menjamin kemudahan verifikasi oleh assessor tanpa Docker.
- **Frontend**: Single Page Application menggunakan React + Vite + TypeScript, ditata dengan modern Vanilla CSS design system (glassmorphism accents, clear contrast, responsive layout, intuitive user switcher).
- **Automated Testing**: xUnit + `Microsoft.AspNetCore.Mvc.Testing` (`WebApplicationFactory`) dengan in-memory / isolated SQLite database untuk integration test end-to-end secara headless dan deterministik.

### 2.2 Relational Data Model (Schema)

#### Table: `users`
| Column | Type | Constraints | Description |
|---|---|---|---|
| `id` | TEXT | PRIMARY KEY | User identifier (e.g. `usr_alice`) |
| `email` | TEXT | NOT NULL UNIQUE | User email (e.g. `alice@example.local`) |
| `name` | TEXT | NOT NULL | Display name |
| `role` | TEXT | NOT NULL | `EMPLOYEE` \| `MANAGER` \| `SYSTEM_OWNER` \| `ADMIN_AUDITOR` |
| `manager_id` | TEXT | REFERENCES `users(id)` | NULL jika tidak memiliki manager |
| `created_at` | DATETIME | NOT NULL DEFAULT CURRENT_TIMESTAMP | Timestamp pembuatan |

#### Table: `applications`
| Column | Type | Constraints | Description |
|---|---|---|---|
| `id` | TEXT | PRIMARY KEY | Application ID (e.g. `app_crm`, `app_fin`) |
| `name` | TEXT | NOT NULL UNIQUE | `CRM` \| `Finance Portal` |
| `system_owner_id` | TEXT | NOT NULL REFERENCES `users(id)` | Owner aplikasi (Carol / Dana) |
| `created_at` | DATETIME | NOT NULL DEFAULT CURRENT_TIMESTAMP | Timestamp pembuatan |

#### Table: `access_requests`
| Column | Type | Constraints | Description |
|---|---|---|---|
| `id` | TEXT | PRIMARY KEY | Request UUID / NanoID |
| `client_request_id` | TEXT | NOT NULL UNIQUE | Idempotency key dari client |
| `requester_id` | TEXT | NOT NULL REFERENCES `users(id)` | Pembuat request |
| `application_id` | TEXT | NOT NULL REFERENCES `applications(id)` | Target aplikasi |
| `environment` | TEXT | NOT NULL CHECK (`environment` IN ('NonProduction', 'Production')) | Target environment |
| `access_level` | TEXT | NOT NULL CHECK (`access_level` IN ('Read', 'Admin')) | Level akses |
| `justification` | TEXT | NOT NULL | Alasan kebutuhan akses |
| `is_high_risk` | INTEGER | NOT NULL CHECK (`is_high_risk` IN (0, 1)) | Flag kalkulasi (1 jika Prod atau Admin) |
| `status` | TEXT | NOT NULL CHECK (`status` IN ('PENDING_MANAGER', 'PENDING_SYSTEM_OWNER', 'APPROVED', 'REJECTED')) | Status workflow |
| `policy_version` | TEXT | NOT NULL DEFAULT 'v1' | Versi kebijakan (`v1`) |
| `current_assignee_id`| TEXT | REFERENCES `users(id)` | User yang ditugaskan mereview saat ini |
| `version` | INTEGER | NOT NULL DEFAULT 1 | Counter concurrency token |
| `reject_reason` | TEXT | NULL | Alasan penolakan (jika status REJECTED) |
| `created_at` | DATETIME | NOT NULL DEFAULT CURRENT_TIMESTAMP | Waktu submit |
| `updated_at` | DATETIME | NOT NULL DEFAULT CURRENT_TIMESTAMP | Waktu modifikasi terakhir |

#### Table: `audit_events`
| Column | Type | Constraints | Description |
|---|---|---|---|
| `id` | INTEGER | PRIMARY KEY AUTOINCREMENT | Sequential event ID |
| `request_id` | TEXT | NOT NULL REFERENCES `access_requests(id)` | Referensi request |
| `actor_id` | TEXT | NOT NULL REFERENCES `users(id)` | User yang mengeksekusi aksi |
| `action` | TEXT | NOT NULL | `SUBMIT` \| `APPROVE` \| `REJECT` |
| `from_status` | TEXT | NULL | Status sebelum aksi |
| `to_status` | TEXT | NOT NULL | Status sesudah aksi |
| `reason` | TEXT | NULL | Catatan / alasan penolakan |
| `request_version` | INTEGER | NOT NULL | Versi request yang dihasilkan |
| `created_at` | DATETIME | NOT NULL DEFAULT CURRENT_TIMESTAMP | Timestamp audit (append-only) |

---

## 3. Implementation Order

1. **Phase 0: Project Setup & Baseline Evidence**
   - Inisialisasi Git repository, `.gitignore`, dan initial commit.
   - Buat git tag: `assessment-start`.
   - Dokumentasikan rancangan awal di `PLAN.md`.

2. **Phase 1: Database Layer & Seed Engine**
   - Setup konfigurasi database SQLite (`db.ts`), PRAGMA enforcement (`foreign_keys = ON`, WAL mode).
   - Buat DDL migration script (tabel `users`, `applications`, `access_requests`, `audit_events`).
   - Buat database seed runner dengan 5 seeded demo users dan 2 aplikasi.

3. **Phase 2: Core Domain Logic & State Machine**
   - Buat module domain workflow (`workflow.ts`):
     - Evaluator `isHighRisk(environment, accessLevel)`.
     - FSM validator: cek transisi legal vs ilegal.
     - Service request creation dengan validasi idempotency (`client_request_id`).
     - Service approve/reject dengan optimisitic concurrency check (`WHERE id = ? AND version = ?`) dan update versi atomik.
     - Pencatatan event audit di dalam transaksi yang sama (`db.transaction`).

4. **Phase 3: Backend API & Authorization Layer**
   - Middleware simulasi autentikasi (`x-user-id` header).
   - Authorization guard:
     - Check: requester tidak boleh approve request sendiri.
     - Check: user harus match dengan `current_assignee_id`.
   - Endpoints:
     - `GET /api/users`: List seeded users untuk switcher.
     - `GET /api/applications`: List master applications.
     - `POST /api/requests`: Submit request baru (idempotent).
     - `GET /api/requests`: List requests (filter by requester atau all untuk admin/auditor).
     - `GET /api/requests/inbox`: List request pending untuk user aktif.
     - `GET /api/requests/:id`: Detail request + riwayat audit timeline.
     - `POST /api/requests/:id/approve`: Action approve dengan parameter `expectedVersion`.
     - `POST /api/requests/:id/reject`: Action reject dengan parameter `expectedVersion` dan `reason`.

5. **Phase 4: Automated Testing Suite**
   - Menulis integration test menggunakan Vitest + Supertest:
     - **Test 1**: Standard flow (Alice -> CRM, NonProd, Read -> Bob approve -> APPROVED).
     - **Test 2**: High-risk flow (Alice -> CRM, Prod, Read -> Bob approve -> PENDING_SYSTEM_OWNER -> Carol approve -> APPROVED).
     - **Test 3**: High-risk admin flow (Alice -> Finance, NonProd, Admin -> Bob approve -> Dana approve -> APPROVED).
     - **Test 4**: Authorization failure (User luar/unauthorized mencoba approve -> 403 Forbidden; Requester mencoba self-approve -> 403 Forbidden).
     - **Test 5**: Idempotency test (Request berulang dengan `ClientRequestId` yang sama menghasilkan ID bisnis yang sama).
     - **Test 6**: Optimistic Concurrency / Conflict test (Dua aksi serentak pada versi yang sama menghasilkan 1 sukses dan 1 error 409 Conflict).
     - **Test 7**: Rejection test (Reject tanpa reason ditolak; reject dengan reason mengubah status jadi REJECTED dan terminal).

6. **Phase 5: Frontend Development**
   - User Switcher Component (memilih Alice, Bob, Carol, Dana, Erin).
   - "New Access Request" form dengan validasi client-side dan server feedback.
   - "My Requests" view untuk melihat status terkini.
   - "Approval Inbox" untuk Manager dan System Owner dengan tombol aksi cepat.
   - "Request Detail & Audit Timeline" menampilkan diagram/timeline status dan event log.
   - Penanganan feedback UI saat terjadi 409 Conflict (menampilkan pesan bahwa data telah diubah oleh approver lain dan perlu direfresh).

7. **Phase 6: Verification, Documentation & Final Tag**
   - Review seluruh requirement checklist.
   - Lengkapi `AI_USAGE.md`, `REVIEW.md`, `INTEGRITY.md`, dan `README.md`.
   - Jalankan seluruh automated test untuk memastikan passing 100%.
   - Buat git tag: `phase-1-complete`.

---

## 4. Test Strategy

| Test Case | Scenario / Skenario Spesifikasi | Expected Result |
|---|---|---|
| `TC-01` | **Standard Request**: Alice -> CRM, NonProd, Read | Bob approve -> Status langsung `APPROVED`, audit event tersimpan. |
| `TC-02` | **Production Request (High-Risk)**: Alice -> CRM, Prod, Read | Bob approve -> Status `PENDING_SYSTEM_OWNER`; Carol approve -> Status `APPROVED`. |
| `TC-03` | **Admin Access Request (High-Risk)**: Alice -> Finance, NonProd, Admin | Bob approve -> Status `PENDING_SYSTEM_OWNER`; Dana approve -> Status `APPROVED`. |
| `TC-04` | **Unauthorized Approval**: User yang bukan assigned approver (misal Dana meng-approve CRM) | Backend merespons `403 Forbidden`, state tidak berubah. |
| `TC-05` | **Self-Approval Attempt**: Alice mencoba meng-approve request miliknya | Backend merespons `403 Forbidden`, state tidak berubah. |
| `TC-06` | **Duplicate Submit (Idempotency)**: Submit berulang dengan `ClientRequestId` yang sama | Return request yang sudah dibuat sebelumnya (HTTP 200/201), tidak ada row ganda di database. |
| `TC-07` | **Concurrent Action (Optimistic Concurrency)**: Dua request approve/reject dengan versi yang sama | Request pertama sukses (HTTP 200), request kedua gagal dengan `409 Conflict`. |
| `TC-08` | **Rejection Flow**: Bob menolak request Alice dengan alasan | Status menjadi `REJECTED`, reject reason tersimpan di audit event dan database, tidak dapat diubah lagi. |

Semua test akan diotomasi dan dapat dijalankan via satu perintah tunggal: `dotnet test`.

---

## 5. Architectural & Technical Trade-offs

1. **Trade-off 1: SQLite (Embedded) via EF Core vs PostgreSQL/MySQL (External Server)**
   - *Pilihan*: SQLite dengan `Microsoft.EntityFrameworkCore.Sqlite` dan WAL mode.
   - *Trade-off*: SQLite tidak dirancang untuk horizontal scaling across multiple distributed nodes.
   - *Justifikasi*: Untuk kebutuhan MVP / take-home assessment, SQLite memberikan zero setup friction bagi assessor (tidak memerlukan docker-compose atau instalasi database server lokal), memiliki kepatuhan ACID penuh, mendukung *strict foreign keys* dan *unique constraints*, serta sangat cepat untuk automated test suites.
2. **Trade-off 2: Simulated Auth Header (`x-user-id`) vs Full OAuth2 / JWT / Session Store**
   - *Pilihan*: Menggunakan header HTTP `x-user-id` yang dipetakan ke seeded users via ASP.NET Core middleware / Authentication handler.
   - *Trade-off*: Tidak memiliki proteksi cryptographic signature seperti JWT atau stateful encrypted session cookie pada production sungguhan.
   - *Justifikasi*: Spesifikasi Section 2.2 secara eksplisit mengizinkan: *"Authentication boleh disimulasikan dengan user switcher / request header / session lokal. Authorization tetap wajib ditegakkan di backend"*. Pendekatan ini mengeliminasi kompleksitas yang tidak diminta (*Section 3.5 sengaja tidak diminta: OAuth/OIDC atau identity provider nyata*), sehingga fokus engineering dialokasikan pada *server-side authorization*, *concurrency control*, dan *state integrity*.
3. **Trade-off 3: Integer Version Concurrency Token vs Timestamp / ETag**
   - *Pilihan*: Monotonically increasing integer `version` (1, 2, 3...) pada entitas `AccessRequest`, diproteksi oleh EF Core `[ConcurrencyCheck]`.
   - *Trade-off*: Membutuhkan kolom versi eksplisit pada skema database.
   - *Justifikasi*: Integer version kebal terhadap masalah *clock skew* atau resolusi sub-milidetik pada server, sangat mudah diverifikasi secara deterministik pada database queries (`WHERE Id = @id AND Version = @version`), memicu `DbUpdateConcurrencyException` jika terjadi conflict, dan terintegrasi secara alami ke dalam audit event log.

---

## 6. Plan Tracking & Changes During Implementation

*(Bagian ini akan diperbarui selama proses implementasi berlangsung jika terdapat penyesuaian teknis)*

- **2026-10-30 12:17**: Initial plan dibuat dan baseline tag `assessment-start` dibuat.
- **2026-10-30 12:30 [Change Request]**: Penyesuaian Requirement dari Kandidat/User: Backend diubah dari Node.js/Express menjadi **.NET 9 (C#) ASP.NET Core Web API** dengan **Entity Framework Core 9 (EF Core)** dan **SQLite**. Frontend tetap menggunakan **React + Vite** untuk user interface rich experience, dan automated test menggunakan **xUnit** (`dotnet test`).

