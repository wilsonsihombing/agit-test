# MVP Access Request Hub (Enterprise Fullstack Assessment)

MVP Access Request Hub adalah sistem manajemen permohonan akses aplikasi internal yang berfungsi sebagai *single source of truth* untuk proses submission, alur approval berjenjang, penanganan konkurensi (optimistic locking), proteksi idempotency, serta *append-only audit trail*.

Solusi ini dibangun menggunakan **ASP.NET Core 9 Web API (.NET 9 C#)** dengan **Entity Framework Core 9** dan **SQLite** pada sisi backend, serta **React + Vite + TypeScript** dengan modern design system pada sisi frontend.

---

## 1. Prerequisites / Persyaratan Sistem
- **.NET SDK**: .NET 9.0+ (`dotnet --version`)
- **Node.js**: v20+ dan npm 10+ (`node -v`, `npm -v`)
- **Git**

---

## 2. Quick Start / Menjalankan Aplikasi

### Opsi A: Menjalankan Backend & Frontend Secara Bersamaan
Buka dua terminal terpisah:

**Terminal 1 (Backend API - Port 5000):**
```bash
dotnet run --project backend/AccessRequestHub.Api/AccessRequestHub.Api.csproj
```
*Backend API akan aktif di `http://localhost:5000`. Database SQLite (`access_hub.db`) dan data seed master otomatis dibuat saat pertama kali dijalankan.*

**Terminal 2 (Frontend UI - Port 5173):**
```bash
cd frontend
npm install
npm run dev
```
*Buka browser di `http://localhost:5173`.*

---

## 3. Menjalankan Automated Tests
Seluruh skenario pengujian bisnis (standard flow, high-risk flow, authorization refusal, self-approval prevention, idempotency duplicate submit, dan optimistic concurrency conflict) dapat dijalankan melalui perintah tunggal:

```bash
# Menjalankan xUnit test suite
dotnet test backend/AccessRequestHub.sln
```
Atau via npm:
```bash
npm test
```

---

## 4. Demo Users & Peran (Seeded Users)

Aplikasi telah dilengkapi dengan data demo pengguna:

| Email | Nama Display | Role / Tanggung Jawab |
|---|---|---|
| `alice@example.local` | Alice | Requester (karyawan bawahan Bob). |
| `bob@example.local` | Bob | Manager untuk Alice (memproses Manager approval). |
| `carol@example.local` | Carol | System Owner untuk aplikasi **CRM**. |
| `dana@example.local` | Dana | System Owner untuk aplikasi **Finance Portal**. |
| `erin@example.local` | Erin | Admin/Auditor (melihat seluruh request & audit timeline). |

*Anda dapat berpindah user secara instan melalui dropdown **Active User Switcher** di bagian header aplikasi.*

---

## 5. Master Data Aplikasi

| Aplikasi | System Owner |
|---|---|
| **CRM** | Carol (`carol@example.local`) |
| **Finance Portal** | Dana (`dana@example.local`) |

*Aturan Risiko Tinggi (High-Risk): Suatu permohonan berstatus **High-Risk** apabila `Environment = Production` ATAU `AccessLevel = Admin`.*

---

## 6. Demo Scenarios Walkthrough (Sesuai Section 2.7)

### Skenario 1: Standard Request
1. Pada User Switcher, pilih **Alice**.
2. Klik tombol **New Request**.
3. Pilih aplikasi **CRM**, Environment **NonProduction**, Access Level **Read**, isi justification, lalu submit.
4. Ganti user switcher ke **Bob** (Manager).
5. Buka tab **Approval Inbox**, klik **Approve**.
6. Status langsung berubah menjadi **Approved** (single-stage).

### Skenario 2: Production Request (High-Risk)
1. Pilih **Alice**, buat request: **CRM**, **Production**, **Read**.
2. Label preview akan menampilkan **HIGH RISK Request**. Submit request.
3. Ganti user switcher ke **Bob**, buka Approval Inbox, klik **Approve**.
4. Status berpindah ke **Pending System Owner** dengan assignee beralih ke **Carol**.
5. Ganti user switcher ke **Carol** (System Owner CRM).
6. Buka Approval Inbox, klik **Approve**.
7. Status permohonan berhasil menjadi **Approved**.

### Skenario 3: Admin Access Request (High-Risk)
1. Pilih **Alice**, buat request: **Finance Portal**, **NonProduction**, **Admin**.
2. Ganti ke **Bob**, approve. Request berpindah menunggu **Dana** (System Owner Finance).
3. Ganti ke **Dana**, approve. Status menjadi **Approved**.

### Skenario 4: Unauthorized Approval Protection
1. Jika user selain approver yang ditugaskan (misal Dana mencoba meng-approve request CRM Carol), backend menolak aksi dengan `403 Forbidden`.

### Skenario 5: Self-Approval Prevention
1. Requester tidak dapat meng-approve permohonan miliknya sendiri meskipun ia memiliki hak otorisasi (ditolak backend dengan `403 Forbidden`).

### Skenario 6: Concurrency Conflict Handling (Optimistic Locking)
1. Jika dua tab atau dua approver melakukan tindakan terhadap permohonan dengan versi yang sama secara berdekatan, transisi pertama berhasil dan transisi kedua menghasilkan `409 Conflict`.
2. UI menampilkan banner peringatan konflik dan tombol refresh data.

### Skenario 7: Rejection Flow
1. Approver dapat menolak request dengan menekan tombol **Reject** dan wajib menyertakan alasan.
2. Permohonan berstatus **Rejected** (terminal) dan alasan penolakan tercatat di dalam audit timeline.

---

## 7. Submission Checklist Verification (Section 6)
- [x] Source frontend + backend dapat dijalankan mengikuti README.
- [x] Database relasional SQLite dapat dibuat ulang otomatis via migration/seed.
- [x] Demo users dan flow simulasi switcher tersedia.
- [x] Automated tests berjalan 100% via `dotnet test` / `npm test`.
- [x] Tag git `assessment-start` dan `phase-1-complete` tersedia.
- [x] Seluruh dokumen evidence (`PLAN.md`, `AI_USAGE.md`, `REVIEW.md`, `INTEGRITY.md`, `README.md`) tersedia lengkap.
- [x] Tidak ada kredensial atau data rahasia perusahaan dalam repository.
