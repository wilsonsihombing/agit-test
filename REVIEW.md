# Production Readiness Self-Review & Risk Analysis

Dokumen ini memuat hasil *self-review* menyeluruh terhadap implementasi MVP Access Request Hub (Phase 1), mencakup temuan keamanan, konsistensi data, mitigasi konkurensi, limitasi yang diketahui, dan pekerjaan yang ditunda (*deferred work*).

---

## 1. Findings & Remediation Matrix

| ID | Finding / Area | Severity | Action Taken | Status | Evidence |
|---|---|---|---|---|---|
| `SEC-01` | **Self-Approval Vulnerability**: Requester dapat meloloskan permohonannya sendiri jika merangkap peran. | **High** | Validasi server-side eksplisit di `AccessRequestService`: `if (actor.Id == request.RequesterId) throw new DomainForbiddenException()`. | **Resolved** | Automated test `Scenario5_SelfApproval_PreventedWithForbidden403` lulus (HTTP 403). |
| `CONC-01` | **Stale / Race Condition pada Approval**: Dua approver mengeksekusi request yang sama pada waktu hampir bersamaan. | **High** | Menerapkan `Version` token dengan EF Core `[ConcurrencyCheck]`. Query update mensyaratkan `WHERE Id = @id AND Version = @expectedVersion`. | **Resolved** | Automated test `Scenario7_ConcurrentAction_OptimisticConcurrency_SecondFailsWithConflict409` lulus (HTTP 409). |
| `IDEM-01` | **Duplicate Request Creation**: Pengiriman berulang atau retry jaringan menghasilkan entri bisnis ganda. | **High** | Kolom `ClientRequestId` diproteksi oleh *Unique Index* pada SQLite. Handler menangkap konflik dan mengembalikan record yang sudah ada. | **Resolved** | Automated test `Scenario6_DuplicateSubmit_Idempotent_ReturnsExistingRecord` lulus (HTTP 200/201). |
| `SEC-02` | **Unauthorized Approval Action**: Approver dari aplikasi lain (misal Dana) mencoba meng-approve aplikasi CRM milik Carol. | **High** | Validasi kepemilikan aplikasi di backend (`actor.Id != request.Application.SystemOwnerId`) sebelum transisi diizinkan. | **Resolved** | Automated test `Scenario4_UnauthorizedApproval_RejectedWithForbidden403` lulus (HTTP 403). |
| `AUD-01` | **Atomic Audit Logging**: Perubahan status tersimpan tetapi audit log gagal, menyebabkan inkonsistensi jejak audit. | **Medium** | Perubahan status entitas `AccessRequest` dan pembuatan row `AuditEvent` dieksekusi dalam transaksi unit of work yang sama (`SaveChangesAsync()`). | **Resolved** | `Scenario1_StandardRequest_ApprovedDirectlyByManager` memverifikasi histori audit tersimpan atomik. |
| `DATA-01` | **Rejection Without Justification**: Penolakan permohonan tanpa alasan menyulitkan klarifikasi bisnis. | **Medium** | Validasi input `RejectRequestDto.Reason` wajib terisi (min length 3 char). Jika kosong, ditolak dengan HTTP 400 Bad Request. | **Resolved** | `Scenario8_RejectedRequest_StoresReasonAndTerminates` memverifikasi validasi alasan penolakan. |

---

## 2. Known Limitations & Production Gaps

1. **Simulated Authentication Header (`x-user-id`)**:
   - *Kondisi Saat Ini*: Menggunakan header HTTP simulasi untuk memudahkan pengujian multi-user oleh penilai (*Section 2.2*).
   - *Kebutuhan Production*: Harus diganti dengan protokol standar enterprise seperti OpenID Connect (OIDC) / OAuth2 yang terhubung ke Active Directory / Okta / Azure AD dengan JWT validation middleware.
2. **Database SQLite Single-File**:
   - *Kondisi Saat Ini*: SQLite dengan Write-Ahead Logging (WAL) mode untuk kemudahan evaluasi tanpa dependensi server database eksternal.
   - *Kebutuhan Production*: Di lingkungan cloud multi-instance (misal Kubernetes/ECS), koneksi database harus dimigrasikan ke PostgreSQL atau SQL Server dengan connection pool terdistribusi.
3. **Audit Trail Immutability Level**:
   - *Kondisi Saat Ini*: Audit event disimpan dalam tabel append-only dengan relasi foreign key ke tabel request.
   - *Kebutuhan Production*: Pada kepatuhan regulasi ketat (SOC2 / ISO 27001), audit log perlu di-streaming ke sistem WORM (Write Once, Read Many) seperti AWS CloudWatch Logs, Datadog, atau database immutability khusus.

---

## 3. Deferred Work (Pekerjaan yang Ditunda ke Phase 2 / Production)

- **Change Request Phase 2**: Menunggu dokumen resmi Change Request Phase 2 dari assessor sesuai instruksi Section 5.
- **Asynchronous Notification Service**: Pengiriman notifikasi email/chat (Slack/Teams) ke approver saat request baru masuk (secara eksplisit masuk daftar *3.5 Sengaja tidak diminta*).
- **Pagination & Advanced Search**: Query filter kompleks berdasarkan rentang tanggal atau text search pada justification (secara eksplisit masuk *3.1 Tidak wajib*).
