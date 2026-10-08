# Praktikum 6 - Behavior Tree & Utility-Based AI

**Nama Kelompok	: (2) Choom**

**Anggota**		    :

- **Triana Velia Hutabalian (5025231190)**
- **Lailatul Annisa Fitriana (5025231202)**
- **Rafaela Shyra Ashma' Ramadhani (5025231217)**
**Kelas           : Game Cerdas (T)**

Bonus yang dikerjakan:
- **Search Last Seen Position** (`EnemyBTController.cs`) — Enemy menyimpan posisi terakhir Player terlihat, menuju ke sana saat Player hilang, melihat sekeliling 3 detik, lalu kembali Patrol.
- **Health Bar & Nameplate** (`HealthNameplate.cs`, `PlayerHUD.cs`, `UIKit.cs`) — nameplate di atas kepala Enemy (nama, chip Current Action, health bar bergradien hijau → kuning → merah dengan *damage trail* putih, flash saat terkena damage, garis penanda Low Health Threshold, dan angka damage melayang). Health Player ditampilkan di kartu bawah layar dengan efek vignette merah saat terkena damage dan overlay "PLAYER DEFEATED". Seluruh UI dibangun lewat kode (uGUI) dengan sprite rounded prosedural.
- **On-screen Behavior Tree Debugger** (`EnemyBTDebugHUD.cs`) — kartu Current Action berwarna, visualisasi pohon keputusan (cabang aktif disorot, tiap condition tampil sebagai pill hijau/abu), bar skor Utility AI dengan indikator SAMA/BEDA terhadap pilihan Behavior Tree, serta warna tubuh Enemy mengikuti action.
- **Animator Integration** (`EnemyAnimatorDriver.cs`, `Assets/Animations/`) — Animator `Idle / Walk / Run / Attack / Flee` pada child `Model`. Parameter: `Speed` (float, dari kecepatan NavMeshAgent → Idle/Walk/Run), `Attack` (trigger, dari event `OnAttack`), `IsFleeing` (bool, dari Current Action = FLEE). Clip dibuat lewat kode (bobbing, lean, lunge) karena Enemy memakai kapsul tanpa model.
- **Personality Enemy** (scene `P06_Personality`) — `AggressiveEnemy` dan `CowardEnemy` memakai Behavior Tree yang sama persis, hanya parameter Inspector yang berbeda:

  | Parameter | AggressiveEnemy | CowardEnemy |
  |---|---|---|
  | Vision Range / Angle | 15 / 150° | 10 / 90° |
  | Chase Speed | 5 | 3 |
  | Attack Range | 2,5 | 2 |
  | Attack Cooldown | 0,8 | 2 |
  | Low Health Threshold | 15 | 70 |
  | Flee Speed | 5 | 6 |

  Kesimpulan: perilaku NPC ditentukan bukan hanya oleh algoritma/struktur tree, tetapi juga oleh parameter tuning.
- **Full Utility AI Controller** (scene `P06_UtilityAI`, `EnemyUtilityController.cs` + `EnemyPerception.cs`) — Enemy memilih Attack/Chase/Flee/Search/Patrol berdasarkan skor tertinggi (lihat bagian di bawah). Perception dipisah ke komponen `EnemyPerception` dari Decision Making.

### Pemulihan health (Recovery)

Agar Enemy tidak terjebak di SafePoint selamanya, Enemy beristirahat dan memulihkan health setelah sampai di SafePoint:
- `healthRegenPerSecond` (8 HP/detik) hanya berlaku saat Enemy sudah tiba di SafePoint, bukan saat masih berlari.
- Behavior Tree: `Is Health Low?` bersifat *latching* — begitu health ≤ threshold, kondisi tetap true sampai health pulih ke `recoverHealthPercent` (80%). Tanpa jeda ini Enemy akan kembali menyerang dengan health 31 lalu langsung kabur lagi (flickering, mirip alasan hysteresis pada FSM P05).
- Utility AI: setelah memilih Flee, skor Flee dijaga tinggi sampai health pulih; setelah itu skor turun dan Enemy kembali memilih Search/Patrol.
- Tombol **J** tetap bisa mengisi health Enemy secara instan untuk keperluan demo.

### Scene yang tersedia

| Scene | Isi |
|---|---|
| `P06_BehaviorTree` | Tugas utama: Enemy Behavior Tree + Search + Health Bar + Animator + BT Debugger |
| `P06_Personality` | AggressiveEnemy vs CowardEnemy |
| `P06_UtilityAI` | Enemy Utility AI (Action Commitment + Hysteresis) |

Scene dibangun ulang lewat menu **Praktikum 06 > Build All Scenes**.

### Behavior Tree dengan Search

```
Root
└── Selector
    ├── Flee Sequence    (Health Low? → Flee)
    ├── Attack Sequence  (Can See? → In Range? → Cooldown → Attack)
    ├── Chase Sequence   (Can See? → Chase)
    ├── Search Sequence  (Has Last Seen Position? → Search)   ← bonus
    └── Patrol
```

Search mengembalikan **Running** selama berjalan menuju titik dan melihat sekeliling, lalu **Failure** setelah selesai (memory dihapus) sehingga Selector langsung jatuh ke Patrol. Memory diperbarui setiap `CanSeePlayer()` bernilai true (`RememberPlayerPosition()`), dan posisi di-snap ke NavMesh dengan `NavMesh.SamplePosition`. Jika Player terlihat lagi saat Search, Attack/Chase (prioritas lebih tinggi) langsung mengambil alih. Posisi memory digambar sebagai gizmo magenta saat Enemy dipilih.

---

## Eksperimen — Utility-Based AI

Skor dihitung di `EnemyBTDebugHUD.ComputeUtilityScores()` (semua bernilai 0..1, dipilih yang tertinggi) dan ditampilkan real-time di HUD berdampingan dengan pilihan Behavior Tree.

| Action | Rumus |
|---|---|
| Attack | `(1 − jarak / (attackRange × 2)) × visibility` |
| Chase | `clamp01(0.2 + 0.6 × jarak / visionRange) × visibility` |
| Flee | `(1 − healthPercent) × threat` (threat = 1 jika Player terlihat, 0,5 jika tidak) |
| Search | `0,4` jika ada memory posisi terakhir dan Player tidak terlihat, selain itu `0` |
| Patrol | `0,1` (default action) |

Contoh perhitungan (attackRange = 2, visionRange = 10):

| Kasus | Attack | Chase | Flee | Patrol | Utility memilih | Behavior Tree memilih |
|---|---|---|---|---|---|---|
| HP 100, Player tidak terlihat | 0,00 | 0,00 | 0,00 | 0,10 | **Patrol** | Patrol |
| HP 100, terlihat, jarak 6 | 0,00 | 0,56 | 0,00 | 0,10 | **Chase** | Chase |
| HP 100, terlihat, jarak 1 | 0,75 | 0,26 | 0,00 | 0,10 | **Attack** | Attack |
| HP 25, terlihat, jarak 1 | 0,75 | 0,26 | 0,75 | 0,10 | seri Attack/Flee (kemungkinan salah satu) | **Flee** (pasti, prioritas) |
| HP 10, terlihat, jarak 1 | 0,75 | 0,26 | 0,90 | 0,10 | **Flee** | Flee |

**Perbandingan.** Behavior Tree bertanya berurutan "Flee mungkin? Attack mungkin? …" lalu berhenti di yang pertama cocok, jadi hasilnya pasti dan mudah ditebak (kasus HP 25 selalu Flee). Utility AI menghitung semua action lalu mengambil skor tertinggi, sehingga perubahan perilaku lebih bertahap (Flee naik pelan seiring HP turun), tetapi dekat titik seri dapat berganti-ganti action (rapid switching) dan perlu hysteresis / action commitment.

### Full Utility AI Controller

Di `EnemyUtilityController` skor dihitung setiap frame, lalu action tertinggi dipilih. Rumus sama dengan eksperimen di atas, dengan perbedaan: Attack hanya bernilai bila Player dalam Attack Range (0,5 – 1,0, makin dekat makin tinggi) dan Flee tidak memakai threshold keras (`1 − healthPercent`).

Dua teknik mencegah *rapid action switching*:
- **Action Commitment** — action aktif dipertahankan minimal `minimumActionDuration` (1 detik) selama skornya masih > 0.
- **Hysteresis** — action baru hanya menggantikan action aktif jika skornya melebihi `skor aktif + switchMargin` (0,15).

Action aktif yang skornya jatuh ke 0 (mis. Player hilang dari pandangan) langsung diganti tanpa menunggu commitment. Ringkasan skor tampil di HUD (`A | C | F | S | P`).

Perbedaan yang bisa diamati dibanding Behavior Tree: pada HP 25 dengan Player berjarak 1, Behavior Tree **selalu Flee** (prioritas), sedangkan Utility AI memiliki skor Attack ≈ Flee (0,75) sehingga tetap Attack dan baru Flee ketika HP lebih rendah (mis. 10 → Flee 0,90).

---

## Analisis Singkat

**1. Apa perbedaan Selector dan Sequence?**
Selector mencoba child berurutan dan berhenti pada child pertama yang **Success/Running** (seperti OR / memilih alternatif); Failure hanya jika semua child gagal. Sequence menjalankan child berurutan dan berhenti pada child pertama yang **Failure/Running** (seperti AND / semua syarat harus terpenuhi); Success hanya jika semua child berhasil.

**2. Apa arti Success, Failure, dan Running?**
- **Success**: node selesai dan berhasil / condition terpenuhi.
- **Failure**: condition tidak terpenuhi atau action gagal.
- **Running**: action belum selesai dan butuh beberapa frame (mis. Chase yang terus bergerak menuju Player, Patrol, atau Attack yang menunggu cooldown). Parent berhenti mengevaluasi sibling lain dan tick berikutnya melanjutkan branch yang sama.

**3. Mengapa urutan child pada Selector penting?**
Urutan = prioritas. Selector berhenti pada child pertama yang tidak Failure, sehingga child di bawahnya tidak dievaluasi. Bila Patrol (yang selalu Running) ditaruh pertama, Enemy hanya akan Patrol dan tidak pernah Flee/Attack/Chase.

**4. Mengapa Flee ditempatkan sebelum Attack?**
Health rendah adalah kondisi darurat. Saat HP ≤ threshold dan Player dekat, syarat Attack juga terpenuhi; bila Attack dievaluasi lebih dulu, Enemy akan tetap menyerang sampai mati. Dengan Flee di posisi pertama, Flee Sequence menghasilkan Running dan Selector tidak pernah mencapai Attack.

**5. Apa fungsi Cooldown Decorator?**
Membungkus satu child (Attack) dan membatasi frekuensi eksekusinya. Tanpa cooldown Attack dipanggil tiap frame (±60 kali/detik). Dengan `nextAllowedTime = Time.time + cooldown`, serangan hanya terjadi tiap 1,5 detik; selama menunggu decorator mengembalikan Running sehingga branch Attack tetap aktif dan Enemy tidak turun ke Chase/Patrol.

**6. Apa perbedaan utama FSM, Behavior Tree, dan Utility AI?**

| Aspek | FSM (P05) | Behavior Tree (P06) | Utility AI |
|---|---|---|---|
| Konsep utama | State aktif + Transition | Tree yang dievaluasi tiap tick | Skor tiap action |
| Cara memilih | Transition antar-state | Node pertama yang memenuhi syarat (urutan Selector) | Skor tertinggi |
| Prioritas | Tersebar di logika transition | Eksplisit lewat urutan child | Hasil perhitungan skor |
| Perubahan perilaku | Diskret | Diskret | Bertahap / halus |
| Skalabilitas | Transition membengkak jika state bertambah | Modular, mudah menambah branch | Modular, perlu tuning skor |
| Debug | Current state | Node/branch aktif | Nilai skor tiap action |

---

## Hasil Pengujian (isi saat demo)

| Uji | Hasil |
|---|---|
| Patrol saat Player tidak terlihat | ☐ |
| Chase saat Player terlihat | ☐ |
| Attack saat Player ≤ Attack Range | ☐ |
| Cooldown (Attack ±1,5 detik sekali, bukan tiap frame) | ☐ |
| Obstacle menghalangi Line of Sight | ☐ |
| FOV (Player di belakang Enemy tidak terlihat) | ☐ |
| Animator: Idle → Walk → Run → Attack → Flee terlihat berganti sesuai action | ☐ |
| Personality: Aggressive lebih cepat melihat/mengejar/menyerang, Coward kabur lebih awal (tekan **H**) | ☐ |
| Utility AI: tidak berganti action bolak-balik di dekat skor seri | ☐ |
| Search: Player bersembunyi di balik Wall → Enemy menuju posisi terakhir, melihat sekeliling, lalu Patrol | ☐ |
| Flee saat HP ≤ 30 (tekan **H** 3× lalu cek) | ☐ |
| Current Action terlihat di HUD / Inspector | ☐ |

**Kontrol:** WASD / panah = gerak Player · **H** = damage Enemy 25 · **J** = reset HP Enemy · **K** = reset HP Player · **R** = restart scene.
