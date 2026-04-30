# ThunderbirdMsgFilterCopy 仕様書

## 0. このドキュメントについて

本ドキュメントは、Thunderbird のメッセージフィルター (`msgFilterRules.dat`) を別環境へ移植するデスクトップツール **ThunderbirdMsgFilterCopy** の仕様書である。開発者と Claude の対話を通じて合意された内容を、Claude Code が実装に着手できる形でまとめている。

## 1. プロジェクト概要

| 項目 | 内容 |
|---|---|
| プロジェクト名 | ThunderbirdMsgFilterCopy |
| 目的 | Thunderbird のメッセージフィルターを別マシン (Windows / macOS) へ安全に移植する |
| 技術スタック | .NET 10 / Avalonia UI / C# (最新言語バージョン) |
| アーキテクチャ | MVVM (CommunityToolkit.Mvvm 推奨) |
| 対応 OS | Windows 10 以降、macOS (Intel / Apple Silicon 両対応) |
| 非対応 OS | Linux (明示的にスコープ外) |
| 配布形態 | 自己完結型 (single-file publish)、OS ごとにビルド |
| ライセンス | 未定 (実装時には MIT を仮置き) |

### 1.1 実装上の重要な注意事項

- **.NET 10 のみを対象とする**。.NET Framework 4.8 向けの API やライブラリ (`System.Web`、古い `WebClient` 等) を混入させないこと。
- **C# 言語は最新バージョン**を使用する。file-scoped namespace、primary constructor、collection expression、required member 等を積極的に活用する。
- **nullable reference types は有効化**する (`<Nullable>enable</Nullable>`)。
- **プラットフォーム分岐**は `OperatingSystem.IsWindows()` / `OperatingSystem.IsMacOS()` を使用する (RuntimeInformation より簡潔)。
- **同期 I/O を避け、async/await を基本**とする。ただし起動時のプロファイル検出のように UI スレッドで完結する軽量処理は同期でも可。
- **Avalonia の XAML は AXAML**。WPF の XAML と微妙に異なる点に注意 (バインディングシンタックスなど)。

### 1.2 開発・ビルド環境

本プロジェクトは以下の IDE でビルド・デバッグが可能であること。いずれか一方専用の機能 (VS 独自プロジェクト型、Rider 独自設定の強要など) に依存してはならない。

| OS      | IDE                              | 用途                                |
|---------|----------------------------------|-------------------------------------|
| Windows | Visual Studio 2022 / 2025 / 2026 | 主たる開発環境                       |
| macOS   | JetBrains Rider                  | macOS 版のビルド・デバッグに使用     |

#### 1.2.1 プロジェクト形式・共通要件

- ソリューション (`.sln`) とプロジェクト (`.csproj`) は **SDK スタイル**で統一し、Visual Studio / Rider の双方でそのまま開けるようにする。
- `TargetFramework` は `net10.0` を指定する。macOS でもまったく同じ TFM でビルドできること (OS 依存は `OperatingSystem.IsWindows()` / `IsMacOS()` のランタイム分岐で対応)。
- パス操作は `Path.Combine` / `Path.DirectorySeparatorChar` を使用し、`\` や `/` のハードコードを禁止する。
- ファイル I/O の既定エンコーディングは **UTF-8 (BOM なし)**、改行コードは **LF** とする。`.editorconfig` で強制する。
- Windows 専用 API (例: `System.Windows.Forms`、レジストリ操作) はコードへ混入させない。どうしても必要な場合は `[SupportedOSPlatform("windows")]` で明示しつつ、共通ロジックから隔離する。

#### 1.2.2 JetBrains Rider 側の留意点

- 起動プロジェクトは `ThunderbirdMsgFilterCopy` (Avalonia アプリ本体) を指定する。
- Rider は Visual Studio が生成する `launchSettings.json` を尊重するため、デバッグプロファイルは共通で利用する。
- Rider で最低限確認すべき動作:
  1. ソリューションを開いてリストア→ビルドが成功する
  2. 実行時に macOS の既定パス (`~/Library/Thunderbird/`) から `profiles.ini` が検出される
  3. AXAML のホットリロード / プレビュー が機能する
  4. テストプロジェクトを Rider の Unit Test Runner から実行できる

#### 1.2.3 .csproj で避けるべき記述

- `<UseWindowsForms>true</UseWindowsForms>` / `<UseWPF>true</UseWPF>` など、Windows 専用 UI フレームワークを有効化するプロパティ。
- Windows 専用の TFM (`net10.0-windows`) をアプリ本体 / Core 側に指定すること。UI 層で Windows 固有 API を使う必要が生じた場合に限り、**条件付き TFM**(`<TargetFrameworks>`) で分岐させる。
- Visual Studio 独自の Guid ベースの古い `csproj` 形式。

## 2. 用語定義

| 用語 | 定義 |
|---|---|
| プロファイル | `profiles.ini` に定義された Thunderbird のユーザープロファイル (`Profiles/xxxxxxxx.default-release/` など) |
| アカウント | プロファイル配下の `Mail/<server>/` または `ImapMail/<server>/` の各ディレクトリ。メッセージフィルターはアカウント単位で保存される |
| フィルターセット | 1 つの `msgFilterRules.dat` に含まれる全フィルター。本ツールはこれを「不可分な 1 単位」として扱い、内容をパースしない |
| エクスポートパッケージ | 本ツールが出力する独自形式ファイル (`.tbfilters`、中身は ZIP) |
| セッション | 1 回のインポート操作。取り消し用のバックアップはセッション単位で保持する |
| Local Folders | Thunderbird が自動生成するローカル専用フォルダ。アカウントに紐づかない特殊領域 |

## 3. 機能要件

### 3.1 プロファイル検出

#### 3.1.1 プロファイルルートの位置

- Windows: `%APPDATA%\Thunderbird\`
- macOS: `~/Library/Thunderbird/`

直下の `profiles.ini` が存在しない場合は「Thunderbird 未インストール」と判定する。

#### 3.1.2 プロファイル選択ロジック (スコアリング方式)

`profiles.ini` と `installs.ini` をパースし、各プロファイル候補に以下の配点でスコアを付ける。UI の初期選択は最高スコアのプロファイル。ただしユーザーは常にドロップダウンから任意のプロファイルを選択できる。

| シグナル | 加点 | 意味 |
|---|---:|---|
| `installs.ini` の `[<InstallHash>]` セクションで `Default=` に参照されている | +100 | Thunderbird 自身が「使用中」と認識 |
| `profiles.ini` で `Default=1` | +50 | 旧仕様の既定プロファイル |
| プロファイル直下に `Mail/` または `ImapMail/` のサブディレクトリが 1 個以上存在 | +30 | メールアカウントが設定されている証拠 |
| 配下に `msgFilterRules.dat` が 1 個以上存在 | +20 | 本ツールの対象データが実在 |
| プロファイル名が `-release` / `-esr` / `-beta` / `-daily` で終わる | +10 | 命名規則ヒント |
| タイブレーカー: 配下の `msgFilterRules.dat` の最新更新日時 | — | 同点時のみ最新のものを選択 |

#### 3.1.3 profiles.ini の形式

INI 形式。例:

```ini
[Profile0]
Name=default-release
IsRelative=1
Path=Profiles/vo4lek9c.default-release

[Profile1]
Name=default
IsRelative=1
Path=Profiles/ola2w6ng.default

[General]
StartWithLastProfile=1
Version=2
```

- `IsRelative=1` の場合、`Path` は `profiles.ini` からの相対パス (区切りは `/`)
- `IsRelative=0` の場合、`Path` は絶対パス

#### 3.1.4 installs.ini の形式

INI 形式。例:

```ini
[FDC34C9F024745EB]
Default=Profiles/vo4lek9c.default-release
Locked=1
```

セクション名はインストールハッシュ。`Default=` に書かれたパスが、そのインストールで使用中のプロファイル。複数セクションがあり得る (複数の Thunderbird インストール)。その場合は `Default=` で参照されている全プロファイルに +100 を加算する。

#### 3.1.5 プロファイル表示 UI

ドロップダウンで全候補を表示。各項目に以下のバッジを付与する:

- `[使用中]`: `installs.ini` でヒットしたプロファイル
- `[空]`: `Mail/` も `ImapMail/` も存在しないプロファイル
- `[フィルター: N件]`: 配下のすべての `msgFilterRules.dat` に含まれる
  個別フィルターの **合計本数**。各ファイル内の `name="..."` 出現回数を
  全アカウント分合算した値とする。`msgFilterRules.dat` のファイル数では
  ない点に注意。

例:

```
プロファイル: [vo4lek9c.default-release ▼]
  ● vo4lek9c.default-release  [使用中] [フィルター: 18件]
  ○ ola2w6ng.default          [空]
```

ここでの「フィルター: 18件」は、エクスポート画面のアカウント一覧に
表示される各行の「(フィルター: N件)」(3.5.3) の総和と**必ず一致する**。
両者は同じ定義 (`name="..."` の出現回数) でカウントされるため、
プロファイル単位の合計とアカウント単位の内訳が常に整合する。

更新タイミングは「3.5.5 アカウント一覧の更新タイミング」と同じ条件で
再評価する (インポート完了 / 取り消し完了 / プロファイル切り替え時)。

### 3.2 アカウント検出

選択されたプロファイル配下で以下をスキャン:

- `Mail/*/msgFilterRules.dat`
  - フォルダ名が `Local Folders` → `accountType: Local`
  - それ以外 → `accountType: Pop`
- `ImapMail/*/msgFilterRules.dat` → `accountType: Imap`

`msgFilterRules.dat` が存在しないディレクトリは列挙対象外 (フィルター未設定のアカウントは表示しない)。

### 3.3 prefs.js パース (アカウント情報の補完)

選択されたプロファイル直下の `prefs.js` を読み、ユーザー名 (メールアドレス) を抽出して UI 表示とマニフェストに含める。

#### 3.3.1 ファイル形式

JavaScript ソースの形をしているが、実体は `user_pref("キー", 値);` の羅列。例:

```javascript
user_pref("mail.server.server1.hostname", "imap.gmail.com");
user_pref("mail.server.server1.userName", "hibara@gmail.com");
user_pref("mail.server.server1.type", "imap");
user_pref("mail.server.server1.directory-rel", "[ProfD]ImapMail/imap.gmail.com");
```

#### 3.3.2 パース方針

本物の JavaScript パーサは不要。正規表現で `user_pref\("([^"]+)",\s*(.+)\);` を抽出すれば十分。値は文字列 (ダブルクォート囲み)、真偽値 (`true`/`false`)、整数のいずれか。本ツールは文字列値のみを扱う。

#### 3.3.3 抽出対象

- `mail.server.serverN.hostname`
- `mail.server.serverN.userName`
- `mail.server.serverN.type` (`imap` / `pop3` / `none`)
- `mail.server.serverN.directory-rel`

`N` は連番 (1, 2, 3, ...)。

#### 3.3.4 アカウント突合

`directory-rel` の末尾パス部分 (例: `ImapMail/imap.gmail.com` の `imap.gmail.com`) とアカウント検出で見つけたディレクトリ名を突合し、対応する `userName` を UI に併記する。

#### 3.3.5 エラー時の挙動

`prefs.js` が読めない、該当エントリが見つからない、パース失敗などの場合は、**ユーザー名欄を空にしてフォールバック**する。ディレクトリ名だけで表示を継続すること。`prefs.js` 関連の失敗を致命的エラーにしてはならない。

### 3.4 Thunderbird 起動状態の監視

#### 3.4.1 検出方法

`Process.GetProcessesByName("thunderbird")` でプロセス存在を確認。Windows / macOS とも `thunderbird` で検出可能。

#### 3.4.2 監視サイクル

- アプリ起動時に 1 回判定
- `System.Threading.PeriodicTimer` で **2 秒間隔** でポーリング
- ViewModel の `IsThunderbirdRunning` プロパティを更新
- UI の操作要素 (ボタン、D&D 受け入れなど) は `IsEnabled = !IsThunderbirdRunning` でバインド

#### 3.4.3 起動検知時の UI

ウィンドウ上部に黄色の警告バナーを常時表示:

> Thunderbird が起動中です。操作を行うには Thunderbird を終了してください。

Thunderbird 終了を検知したらバナーは自動で消える。

#### 3.4.4 自動終了機能

**v1 では実装しない**。ユーザーに手動終了を促すのみ。

### 3.5 エクスポート機能

#### 3.5.1 画面構成

```
┌────────────────────────────────────────────────────┐
│ エクスポート対象のアカウントを選択してください      │
│                                                     │
│ [全選択] [全解除]                                   │
│                                                     │
│ ┌─────────────────────────────────────────────┐   │
│ │ ☐ [IMAP]  hibara@gmail.com                               │   │
│ │          imap.gmail.com              (フィルター: 12件)   │   │
│ │ ☐ [IMAP]  hibara.work@gmail.com                          │   │
│ │          imap.gmail.com-1             (フィルター: 3件)   │   │
│ │ ☐ [POP]   hibara@example.com                             │   │
│ │          pop.example.com              (フィルター: 5件)   │   │
│ │ ☐ [Local] (ローカル)                                     │   │
│ │          Local Folders                (フィルター: 2件)   │   │
│ └─────────────────────────────────────────────┘   │
│                                                     │
│                          [エクスポート...]          │
└────────────────────────────────────────────────────┘
```

#### 3.5.2 動作仕様

- 初期状態: **全チェックボックスが未選択**
- `[全選択]` ボタン: すべてのアカウントをチェック
- `[全解除]` ボタン: すべてのアカウントのチェックを外す
- `[エクスポート...]` ボタン: 1 件以上選択されているときのみ有効化
- ボタン押下時: `SaveFileDialog` を表示。デフォルトファイル名は `thunderbird-filters-yyyyMMdd.tbfilters`、フィルタは `*.tbfilters`
- 保存先が決まったら ZIP ファイルを生成 (後述の「4. ファイル仕様」参照)
- フィルター件数 0 のアカウントは一覧に表示しない
- **アカウント一覧の各行はクリック領域全体でチェック状態をトグルする**。チェックボックス部分のみが反応する挙動は不可。詳細は「5.6 チェックリストの操作性」を参照。

#### 3.5.3 フィルター件数のカウント方法

`msgFilterRules.dat` を UTF-8 テキストとして読み、`name="..."` で始まる行の数をカウントする。中身のパースは不要。

#### 3.5.4 エクスポート結果ダイアログ

ZIP ファイル書き出し完了後に表示する。

```
┌────────────────────────────────────────────────────┐
│  ✓ エクスポートが完了しました                       │
│                                                     │
│  3 アカウント分のメッセージフィルターを出力しました│
│  (合計 20 件のフィルター)                           │
│                                                     │
│  保存先: C:\Users\hibara\Desktop\                   │
│          thunderbird-filters-20260419.tbfilters    │
│                                                     │
│                                          [閉じる]   │
└────────────────────────────────────────────────────┘
```

**文言ルール (重要)**:

- 「1 件」「3 件」のような単位だけの表現は禁止。読み手が「フィルター 1 本」なのか「アカウント 1 個分」なのか判別できないため。
- 必ず **「N アカウント分のメッセージフィルター」** のように **何を数えた件数なのかを名詞で明示する**。
- 出力アカウント数と、その中に含まれる総フィルター件数 (`msgFilterRules.dat` 内の `name="..."` の総和) の両方を表示する。
- 単数・複数で文言を変えない (日本語では不自然になるため、常に同じ文面)。
- 保存先パスはフルパスを表示し、長い場合はダイアログをリサイズ可能にするか、末尾省略 + ツールチップで全体表示にする。

#### 3.5.5 アカウント一覧の更新タイミング

エクスポート画面のアカウント一覧に表示されるフィルター件数 (`(フィルター: N件)`) は、**常にディスク上の `msgFilterRules.dat` の最新状態を反映する**こと。アプリ起動中にインポートや取り消しによってフィルター件数が変化したにもかかわらず、表示が古いまま残ることは仕様違反とする。

具体的には、少なくとも以下のイベントが発生した直後に、**選択中のプロファイル配下のアカウント一覧を再走査して件数を取り直し、UI を更新**しなければならない:

| イベント | 更新理由 |
|---|---|
| インポートの実行が完了した時 | 上書き対象アカウントのフィルター件数が変化する |
| 「直前のインポートを取り消す」が完了した時 | 復元先アカウントのフィルター件数が元に戻る (削除も含む) |
| プロファイルのドロップダウンを切り替えた時 | 別プロファイルのアカウント / 件数に切り替える (5.3 と整合) |

判定基準: 「アプリを再起動した直後の表示」と、上記イベント完了直後の表示が**完全に一致する**こと。一致していなければバグとみなす。

実装の選択肢は規定しない。`ExportViewModel` に `RefreshAccounts()` のような明示メソッドを設けて呼び出してもよいし、`MainWindowViewModel` を仲介役にしてもよいし、`WeakReferenceMessenger` 等のメッセージングを使ってもよい。要件は「最新状態を反映すること」のみ。

なお、件数 0 のアカウントは一覧に表示しない (3.5.2 参照) ため、再走査の結果として「以前は表示されていた行が消える」「表示されていなかった行が新規に出現する」ことも起こり得る。これは正しい挙動である。

### 3.6 インポート機能

#### 3.6.1 画面構成 (初期状態)

```
┌────────────────────────────────────────────────────┐
│  ┌──────────────────────────────────────────────┐ │
│  │                                                │ │
│  │   ここに .tbfilters ファイルを                │ │
│  │     ドラッグ&ドロップ                         │ │
│  │                                                │ │
│  │       または [ファイルを選択...]               │ │
│  │                                                │ │
│  └──────────────────────────────────────────────┘ │
└────────────────────────────────────────────────────┘
```

- ドラッグ&ドロップまたはファイル選択ダイアログから `.tbfilters` を受け付ける
- 複数ファイルドロップは最初の 1 個のみ処理、ファイル以外 (フォルダ等) は拒否
- 拡張子が `.tbfilters` 以外でも ZIP として正常に開け、manifest.json が妥当なら受理 (将来拡張性のため緩めに)

#### 3.6.2 プレビュー画面 (ファイル受理後)

```
┌────────────────────────────────────────────────────┐
│ エクスポート元: Windows / default-release          │
│ エクスポート日時: 2026-04-19 12:34                 │
│                                                     │
│ ┌─────────────────────────────────────────────┐   │
│ │ エクスポート元            → インポート先      │   │
│ │─────────────────────────────────────────────│   │
│ │ imap.gmail.com (IMAP)     → imap.gmail.com   │   │
│ │   hibara@gmail.com                            │   │
│ │   ✓ マッチ (フィルター 12件を上書き)         │   │
│ │─────────────────────────────────────────────│   │
│ │ pop.example.com (POP)     → —                 │   │
│ │   hibara@example.com                          │   │
│ │   ✗ 対応先なし (スキップ)                     │   │
│ │─────────────────────────────────────────────│   │
│ │ ⚠ version="10" (現在の環境は version="9")     │   │
│ └─────────────────────────────────────────────┘   │
│                                                     │
│            [キャンセル]  [実行する]                 │
└────────────────────────────────────────────────────┘
```

#### 3.6.3 マッチングアルゴリズム

**`serverFolderName` と `accountType` の完全一致のみ**。

- マッチ成立: エクスポート元の `serverFolderName` と同じディレクトリ名が、同じ `accountType` (Mail/ImapMail 区別) でインポート先に存在する
- マッチ不成立: 上記以外 → プレビューで「対応先なし」、スキップ

手動マッピングやホスト名部分一致は **v1 では実装しない**。

#### 3.6.4 バージョン互換性チェック

- エクスポートパッケージ内の各 `msgFilterRules.dat` の先頭 `version="..."` 属性値と、インポート先の既存 `msgFilterRules.dat` の `version` を比較
- 不一致があればプレビュー画面に警告マーク (⚠) を表示
- **警告のみで、実行自体は可能**
- インポート先に既存ファイルがない場合はチェックしない

version の抽出も正規表現で `version="(\d+)"` を先頭から検索すれば十分。

#### 3.6.5 実行前確認ダイアログ

「実行する」ボタン押下時に必ず表示する:

```
┌────────────────────────────────────────────────────┐
│  インポートを実行します。                           │
│                                                     │
│  フィルターの「移動先フォルダ」がインポート先の     │
│  Thunderbird に存在しない場合、そのフィルターは     │
│  動作しません。                                     │
│                                                     │
│  問題があった場合は、メニューから「直前のインポート│
│  を取り消す」で元に戻せます。                       │
│                                                     │
│  続行しますか?                                      │
│                                                     │
│                   [キャンセル]   [実行する]         │
└────────────────────────────────────────────────────┘
```

ボタン配置は「キャンセルが左、肯定 (実行する) が右」とする。詳細は「5.7 ダイアログのボタン配置」を参照。

#### 3.6.6 実行処理フロー

1. Thunderbird 起動状態を再確認 (ポーリングの間隙にユーザーが起動した可能性)
2. 既存のバックアップセッション (後述の 3.7 参照) があれば削除
3. 新規バックアップセッションディレクトリを作成
4. マッチした各エントリについて:
   a. インポート先の既存 `msgFilterRules.dat` をバックアップディレクトリへコピー
   b. 既存ファイルが無い場合は `hadExistingFile: false` をセッションメタデータに記録
   c. パッケージ内の `msgFilterRules.dat` をインポート先に上書きコピー
5. 全件完了後、`session.json` を保存
6. **エクスポート画面のアカウント一覧を再走査して件数表示を更新する** (3.5.5 参照)
7. **`HasBackupSession` を再評価して true にし、「直前のインポートを取り消す」メニューを有効化する** (3.7.6 参照)
8. 結果ダイアログを表示 (成功 N 件 / スキップ M 件)
9. エラーが発生したエントリはスキップして次へ。最後にまとめてエラー報告

**途中失敗時のトランザクションロールバックは行わない**。個別のエラーは「直前のインポートを取り消す」機能で復旧する。

#### 3.6.7 結果ダイアログ

```
┌────────────────────────────────────────────────────┐
│  ✓ インポートが完了しました                         │
│                                                     │
│  2 アカウント分のメッセージフィルターを上書きしま  │
│  した (スキップ: 1 アカウント)                      │
│                                                     │
│  Thunderbird を起動して、フィルターが正しく動作    │
│  することを確認してください。                       │
│                                                     │
│  問題があった場合は、メニューから「直前のインポート│
│  を取り消す」で元に戻せます。                       │
│                                                     │
│                                          [閉じる]   │
└────────────────────────────────────────────────────┘
```

**「元に戻す」ボタンは置かない**。復旧はメニュー経由のみ。

文言は「3.5.4 エクスポート結果ダイアログ」と同じルールに従い、**単位だけの「N 件」表現は使わない**。必ず「N アカウント分のメッセージフィルター」のように対象を明示する。

### 3.7 直前のインポートを取り消す機能

#### 3.7.1 バックアップの保存先

- Windows: `%LOCALAPPDATA%\ThunderbirdMsgFilterCopy\Backup\`
- macOS: `~/Library/Application Support/ThunderbirdMsgFilterCopy/Backup/`

ディレクトリが存在しなければ初回インポート時に自動作成する。

#### 3.7.2 バックアップ構造

**直前 1 セッションのみ保持**。次回インポート時に前のバックアップは丸ごと削除してから新規作成する。

```
Backup/
  session.json
  files/
    0000/msgFilterRules.dat     ← imap.gmail.com の元ファイル
    0001/msgFilterRules.dat     ← Local Folders の元ファイル
```

#### 3.7.3 session.json の構造

```json
{
  "executedAt": "2026-04-19T12:34:56+09:00",
  "profilePath": "C:\\Users\\hibara\\AppData\\Roaming\\Thunderbird\\Profiles\\vo4lek9c.default-release",
  "entries": [
    {
      "index": 0,
      "accountType": "Imap",
      "serverFolderName": "imap.gmail.com",
      "originalFilePath": "...\\ImapMail\\imap.gmail.com\\msgFilterRules.dat",
      "backupFilePath": "files/0000/msgFilterRules.dat",
      "hadExistingFile": true
    }
  ]
}
```

- `hadExistingFile: false` のエントリは、**復元時にインポート先のファイルを削除**する (元々無かった状態に戻す)
- `originalFilePath` は絶対パスで保存する

#### 3.7.4 UI

メインウィンドウのメニューに「**直前のインポートを取り消す**」項目を常設する。

- バックアップセッションが無ければグレーアウト
- Thunderbird 起動中もグレーアウト (起動状態が変わったら動的に切り替え)
- クリックで確認ダイアログ表示:

```
┌────────────────────────────────────────────────────┐
│  直前のインポートを取り消しますか?                  │
│                                                     │
│  2026-04-19 12:34 に実行したインポート (2件) を    │
│  取り消し、元の状態に戻します。                     │
│                                                     │
│                  [キャンセル]    [取り消す]         │
└────────────────────────────────────────────────────┘
```

ボタン配置は「キャンセルが左、肯定 (取り消す) が右」とする。詳細は「5.7 ダイアログのボタン配置」を参照。

#### 3.7.5 取り消し処理フロー

1. Thunderbird 起動状態を再確認
2. `session.json` の各エントリについて:
   - `hadExistingFile: true` なら、バックアップファイルを `originalFilePath` に上書きコピー
   - `hadExistingFile: false` なら、`originalFilePath` にあるファイルを削除
3. `Backup/` ディレクトリ全体を削除
4. **エクスポート画面のアカウント一覧を再走査して件数表示を更新する** (3.5.5 参照)
5. **`HasBackupSession` を再評価して false にし、「直前のインポートを取り消す」メニューを無効化する** (3.7.6 参照)
6. 完了ダイアログを表示

#### 3.7.6 メニュー有効化状態の更新タイミング

「直前のインポートを取り消す」メニューの有効化条件は **`HasBackupSession && !IsThunderbirdRunning`** である (5.5 参照)。`HasBackupSession` は **バックアップディレクトリの存在 (`session.json` を含む有効なバックアップセッションがあるか)** をもとに評価する真偽値であり、以下のタイミングで再評価し UI に反映しなければならない:

| イベント | `HasBackupSession` の遷移 | 理由 |
|---|---|---|
| アプリ起動時 | バックアップ実在に応じて初期値を決定 | 既存仕様 |
| インポートの実行が完了した時 | true へ | 新たなバックアップセッションが作成されたため。**最も典型的な「取り消したいタイミング」のため、ここでメニューが無効のままでは仕様違反** |
| 「直前のインポートを取り消す」が完了した時 | false へ | `Backup/` ディレクトリが削除されたため |

判定基準: 「アプリを再起動した直後のメニュー状態」と、上記イベント完了直後のメニュー状態が**完全に一致する**こと (3.5.5 と同じ思想)。

`IsThunderbirdRunning` の方は既存の `ThunderbirdMonitor` の 2 秒ポーリングで動的に切り替わる (3.4.2 参照) ため、別途の対応は不要。`HasBackupSession` の更新は件数更新と同じ箇所 (`ImportViewModel` のインポート完了ハンドラ末尾、および取り消しコマンドハンドラ末尾) で同時に行うのが素直。

## 4. ファイル仕様

### 4.1 エクスポートパッケージ `.tbfilters`

拡張子 `.tbfilters`、実体は ZIP アーカイブ。圧縮レベルは既定 (Optimal)。

#### 4.1.1 内部構造

```
manifest.json
filters/
  0000/msgFilterRules.dat
  0001/msgFilterRules.dat
  ...
```

- `filters/` 配下は 4 桁ゼロ詰めの連番ディレクトリ
- `msgFilterRules.dat` はオリジナルの中身をバイナリ (UTF-8 バイト列) でそのまま格納

### 4.2 manifest.json スキーマ

```json
{
  "schemaVersion": 1,
  "exportedAt": "2026-04-19T12:34:56+09:00",
  "sourceOs": "Windows",
  "sourceProfile": "default-release",
  "entries": [
    {
      "index": 0,
      "accountType": "Imap",
      "serverFolderName": "imap.gmail.com",
      "userName": "hibara@gmail.com",
      "fileVersion": "9",
      "filterCount": 12
    }
  ]
}
```

| フィールド | 型 | 説明 |
|---|---|---|
| `schemaVersion` | integer | 現在は 1 固定。将来のスキーマ変更時にインクリメント |
| `exportedAt` | string (ISO 8601) | エクスポート実行日時 (タイムゾーン付き) |
| `sourceOs` | string | `"Windows"` / `"macOS"` |
| `sourceProfile` | string | エクスポート元プロファイル名 (`profiles.ini` の `Name`) |
| `entries[].index` | integer | `filters/NNNN/` の NNNN に対応 |
| `entries[].accountType` | string | `"Imap"` / `"Pop"` / `"Local"` |
| `entries[].serverFolderName` | string | 元のディレクトリ名 (マッチングのキー) |
| `entries[].userName` | string \| null | prefs.js から抽出したユーザー名。取得できなければ null |
| `entries[].fileVersion` | string | `msgFilterRules.dat` 先頭の `version` 属性値 |
| `entries[].filterCount` | integer | `name="..."` 出現回数でカウント |

**重要**: `userName` は UI 表示専用。マッチング判定には絶対に使わない。

### 4.3 msgFilterRules.dat 自体の取り扱い

- UTF-8 テキスト
- 先頭行に `version="9"` 等のヘッダ
- 以下、`name="..."` から次の `name="..."` までが 1 フィルター
- **本ツールは内容をパースしない**。`version` と `name` 出現回数のみ抽出

## 5. UI 仕様

### 5.1 ウィンドウレイアウト

```
┌──────────────────────────────────────────────────┐
│ メニュー: [ファイル] [ツール] [ヘルプ]            │
├──────────────────────────────────────────────────┤
│ [状態バナー (起動中のときのみ表示)]               │
├──────────────────────────────────────────────────┤
│ [タブ: Export] [タブ: Import]                     │
│                                                   │
│   (タブごとのコンテンツ)                           │
│                                                   │
├──────────────────────────────────────────────────┤
│ OS: Windows  |  Profile: default-release ▼       │
└──────────────────────────────────────────────────┘
```

### 5.2 メニュー構成

- **ファイル**
  - 終了
- **ツール**
  - 直前のインポートを取り消す (3.7 参照)
- **ヘルプ**
  - バージョン情報

### 5.3 プロファイル選択

ステータスバーのドロップダウンはアプリ全体で共有。切り替えたら Export / Import タブの内容を再構築する。

### 5.4 Thunderbird 未検出時

- `profiles.ini` が存在しない、または有効なプロファイルが 1 つも無い場合
- タブコントロール全体を無効化
- 中央に大きく「Thunderbird のプロファイルが見つかりませんでした」とメッセージ

### 5.5 操作ロックのバインディング

- `IsThunderbirdRunning` が true → 全操作無効化
- `HasValidProfile` が false → タブ無効化
- `HasBackupSession` が false → 「直前のインポートを取り消す」メニュー無効化

`HasBackupSession` はアプリ起動時の評価だけでは不十分。インポート完了時 / 取り消し完了時に再評価して UI に反映する必要がある (3.7.6 参照)。特に **インポート完了直後にメニューが無効のままになる挙動は仕様違反**であり、最も取り消したいタイミングが封じられてしまう。

### 5.6 チェックリストの操作性

エクスポート画面のアカウント選択リストのように、チェックボックス付きのリストを表示する箇所では、以下を満たすこと。

#### 5.6.1 クリック領域

- **行 (アイテム) の表示領域全体**をクリックすることでチェック状態をトグルできる。
- チェックボックス部分のみを反応させ、テキスト部分をクリックしてもチェックが変化しない挙動は**仕様違反**とする。
- タッチ操作・マウス操作・キーボード操作 (Space キー) のいずれでも同じ結果になること。

#### 5.6.2 視覚的フィードバック

- マウスカーソルが行上にあるときはホバーハイライトを表示する。
- フォーカスがある行は、Avalonia の既定のフォーカス枠 (または同等の視覚表現) を維持する。
- 押下中 (Pressed) はホバーより若干濃い背景にするなど、クリックが反応していることが見て分かるようにする。

#### 5.6.3 実装上のヒント (参考)

- Avalonia で `ListBox` + `CheckBox` を組み合わせる場合、`CheckBox` の `HitTestVisible` を抑える/`IsHitTestVisible="False"` にしたうえで、行全体の `Tapped` イベントで `IsChecked` をトグルする、あるいは `ListBox.SelectionMode="Multiple"` と `IsSelected` を `IsChecked` にバインドする方式を採る。
- `ItemsControl` のアイテムテンプレートは、`HorizontalAlignment="Stretch"` かつ内側のレイアウトが余白まで含めてヒットテスト対象になるよう、背景を `Transparent` で塗る (null だとヒットしない)。
- カスタムコントロールにする場合は `Button` の `ClickMode="Release"` を土台にしてトグルボタン化するのが簡潔。

### 5.7 ダイアログのボタン配置

確認・実行系ダイアログのボタン配置は **OS 共通で統一**する。Windows と macOS でレイアウトを切り替えることはしない。

#### 5.7.1 配置ルール

- **左側**: 否定・キャンセル系ボタン (`キャンセル` / `いいえ` / `閉じる` 等)
- **右側**: 肯定・実行・デフォルト系ボタン (`実行する` / `取り消す` / `OK` / `はい` 等)
- 単一ボタン (情報通知や結果ダイアログの `[閉じる]` `[OK]` 等) はダイアログ右下に配置
- ボタンは水平方向に並べ、`HorizontalAlignment="Right"` で右寄せ。ボタン間の間隔は 8px を基本とする

これは macOS の Apple HIG (肯定が右) に揃えた配置である。Windows は公式ガイドラインで「肯定が左」を定めていないこと、近年の Microsoft 製アプリ (Edge / Teams / Office / Windows 11 設定等) や主要 Web フレームワーク (Material Design / Fluent / Bootstrap 等) も「肯定が右」に寄せていることから、両 OS で統一しても Windows ユーザーへの違和感は小さいと判断する。両 OS を併用するユーザーが OS ごとに配置の違いに戸惑わないことを優先する。

#### 5.7.2 キーボード操作

- **Esc キー**: 常にキャンセル系ボタンに紐づける (`IsCancel="True"`)
- **Enter キー**: 常にデフォルト (肯定) ボタンに紐づける (`IsDefault="True"`)
- 両 OS で同じキーバインドが機能すること

#### 5.7.3 破壊的操作の文言

「はい / いいえ」のような汎用的な肯定/否定よりも、**動詞で何が起きるかを明示**したラベルを優先する。例:

- 良い: `[キャンセル]   [実行する]` / `[キャンセル]   [取り消す]` / `[キャンセル]   [削除する]`
- 避ける: `[はい]   [いいえ]` (操作内容がボタンから読み取れない)

これによりユーザーがボタンのラベルだけで自分が何をしようとしているかを判断できる。

#### 5.7.4 適用対象

本ルールは以下のすべてのダイアログに適用する:

- インポート実行前の確認ダイアログ (3.6.5)
- 「直前のインポートを取り消す」確認ダイアログ (3.7.4)
- エクスポート結果ダイアログ (3.5.4)、インポート結果ダイアログ (3.6.7) などの単一ボタン情報ダイアログ
- About ダイアログ (8.2.5) の `OK` ボタン
- その他、将来追加されるすべての確認・通知ダイアログ

### 5.8 ダイアログのウィンドウサイズ

確認・通知系ダイアログのウィンドウ幅は、本文の長さに依存して縮みすぎないよう **最低幅を確保する**こと。本文が短いダイアログで `SizeToContent="WidthAndHeight"` を素直に適用すると、Windows 環境ではタイトルバー右側のウィンドウコントロール (最小化・最大化・閉じる) が常に約 130px を占有する都合で、タイトル文字列の表示領域がほぼ消失し、`ThunderbirdMsgFilterCopy` のような長めのタイトルが「Th...」程度まで切り詰められてしまう。これは仕様違反とする。

#### 5.8.1 共通サイズ規則

5.7.4 で列挙した確認・通知系ダイアログ (About ダイアログを除く) は、以下のサイズ規則で統一する:

| 項目 | 値 |
|---|---|
| 幅 | **460px (固定)** |
| 高さ | 内容に応じて自動調整 (`SizeToContent="Height"`) |
| リサイズ | 不可 (`CanResize="False"`) |
| 表示位置 | オーナーウィンドウ中央 (`WindowStartupLocation="CenterOwner"`) |
| タスクバー表示 | しない (`ShowInTaskbar="False"`) |

460px は以下を根拠に決定:

- ウィンドウタイトル `ThunderbirdMsgFilterCopy` (英語) ・ `ThunderbirdMsgFilterCopy` (日本語版でも同タイトル) の表示に必要な幅 (約 250px) と、Windows のウィンドウコントロール領域 (約 130px) およびアイコン・パディングを合算しても十分に余裕がある
- エクスポート結果・インポート結果ダイアログ (3.5.4 / 3.6.7) で表示する「N アカウント分のメッセージフィルターを出力しました (合計 M 件のフィルター)」の本文や保存先パス表示も自然に収まる
- About ダイアログ (8.2.5) の固定幅 340 とは別系統。About はアイコン + 短いテキストのみのレイアウトのため独自サイズで問題ない

#### 5.8.2 適用対象

本規則は以下のダイアログに適用する:

- インポート実行前の確認ダイアログ (3.6.5)
- 「直前のインポートを取り消す」確認ダイアログ (3.7.4)
- 「直前のインポートを取り消す」完了ダイアログ (3.7.5)
- エクスポート結果ダイアログ (3.5.4)
- インポート結果ダイアログ (3.6.7)
- エラーダイアログ (6.3) のうち、ウィンドウとしてポップアップさせるもの
- その他、将来追加されるすべての確認・通知系ダイアログ

About ダイアログ (8.2.5) はこの規則の対象外であり、従来どおり 340 × 320 の固定サイズを維持する。

#### 5.8.3 本文が異常に長い場合

エラー詳細 (例外メッセージのコピペ表示など) で 460px 幅では収まり切らないテキストが出るケースは、以下のいずれかで対応する:

- `TextWrapping="Wrap"` で折り返し、高さ方向に伸ばす (`SizeToContent="Height"` のため自動調整される)
- 詳細領域は `ScrollViewer` 内に収め、ダイアログ自身の高さに上限を設ける

ダイアログの幅自体を 460px から拡張することは原則行わない。OS 共通の見た目を保つことを優先する。

## 6. エラーハンドリング

### 6.1 ロギング

**ログファイルは一切出力しない**。全エラーはダイアログ表示のみで完結させる。

### 6.2 エラーパターン

| エラー | 対応 |
|---|---|
| `profiles.ini` パース失敗 | Thunderbird 未検出と同じ扱い |
| `installs.ini` 不在 / パース失敗 | スコアから +100 項目を除外、警告なし |
| `prefs.js` 読み取り失敗 | ユーザー名欄を空にしてフォールバック、警告なし |
| `.tbfilters` の ZIP 展開失敗 | 「ファイルが破損しているか、本ツールで作成されたものではありません」 |
| `manifest.json` 不在 / 不正 | 同上 |
| `schemaVersion` が未サポート | 「このファイルは新しいバージョンのツールで作成されました」 |
| ファイル書き込み権限なし | 「書き込み権限がありません。Thunderbird プロファイルのアクセス権を確認してください」 |
| バックアップディレクトリ作成失敗 | エラーダイアログを表示し、インポート処理全体を中止 |

### 6.3 エラーダイアログの内容

- 一般ユーザー向けの日本語メッセージを主文に
- 詳細 (例外メッセージ) は「詳細」ボタン展開で表示 (コピペ可能にする)
- スタックトレースは含めない

## 7. プロジェクト構成案

```
ThunderbirdMsgFilterCopy.sln
├── ThunderbirdMsgFilterCopy/           # Avalonia アプリ本体
│   ├── App.axaml
│   ├── Program.cs
│   ├── ViewModels/
│   │   ├── MainWindowViewModel.cs
│   │   ├── ExportViewModel.cs
│   │   ├── ImportViewModel.cs
│   │   └── ImportPreviewViewModel.cs
│   ├── Views/
│   │   ├── MainWindow.axaml
│   │   ├── ExportView.axaml
│   │   ├── ImportView.axaml
│   │   └── ImportPreviewView.axaml
│   └── Services/
│       ├── IThunderbirdMonitor.cs        / ThunderbirdMonitor.cs
│       ├── IProfileDetector.cs           / ProfileDetector.cs
│       ├── IAccountScanner.cs            / AccountScanner.cs
│       ├── IPrefsJsParser.cs             / PrefsJsParser.cs
│       ├── IFilterExporter.cs            / FilterExporter.cs
│       ├── IFilterImporter.cs            / FilterImporter.cs
│       └── IBackupManager.cs             / BackupManager.cs
├── ThunderbirdMsgFilterCopy.Core/       # プラットフォーム非依存ロジック
│   ├── Models/
│   │   ├── ThunderbirdProfile.cs
│   │   ├── ThunderbirdAccount.cs
│   │   ├── ExportManifest.cs
│   │   ├── ManifestEntry.cs
│   │   └── BackupSession.cs
│   └── (上記 Services の実装も可能ならここに移す)
└── ThunderbirdMsgFilterCopy.Tests/      # xUnit 等の単体テスト
```

### 7.1 主要クラスの責務

| クラス | 責務 |
|---|---|
| `ThunderbirdMonitor` | 2 秒間隔でプロセス存在を確認、`IsRunning` を `INotifyPropertyChanged` で通知 |
| `ProfileDetector` | `profiles.ini` / `installs.ini` をパースし、スコアリング済みのプロファイルリストを返す |
| `AccountScanner` | 指定プロファイル配下の `Mail/` `ImapMail/` をスキャンしてアカウント一覧を返す |
| `PrefsJsParser` | `prefs.js` から `mail.server.serverN.*` を抽出、ディレクトリ名からユーザー名を逆引き |
| `FilterExporter` | 選択されたアカウントから `.tbfilters` ZIP を生成 |
| `FilterImporter` | `.tbfilters` を解析し、マッチング結果を返す + 実行時はコピー処理を実施 |
| `BackupManager` | 直前 1 セッションのバックアップ作成・取り消し・削除 |

## 8. 非機能要件

### 8.1 性能・リソース要件

- **起動時間**: 1 秒以内 (プロファイル検出含む)
- **メモリ使用量**: 通常運用で 100 MB 以下
- **ファイルサイズ**: 自己完結型配布時 100 MB 以下目標 (Avalonia の依存次第で変動)
- **多言語対応**: v1 は日本語のみ。ただしリソース分離設計にして将来の国際化に備える

### 8.2 アセンブリ情報・バージョニング

アセンブリ情報 (製品名、バージョン、著作権など) の記述場所と運用ルールを定める。

#### 8.2.1 基本方針

- アセンブリ情報はすべて **`.csproj` の `<PropertyGroup>` に記述する**
- **`AssemblyInfo.cs` は作成しない**。.NET SDK が自動生成するため、独自に用意するとビルド時に重複定義エラーとなる
- `Properties/AssemblyInfo.cs` ファイルが生成された場合は削除すること

#### 8.2.2 .csproj に記述するプロパティ

最低限以下を設定する:

```xml
<PropertyGroup>
  <AssemblyName>ThunderbirdMsgFilterCopy</AssemblyName>
  <RootNamespace>ThunderbirdMsgFilterCopy</RootNamespace>
  <Product>ThunderbirdMsgFilterCopy</Product>
  <AssemblyTitle>ThunderbirdMsgFilterCopy</AssemblyTitle>
  <Description>Thunderbird のメッセージフィルターを別環境へ移植するツール</Description>
  <Company>Hibara</Company>
  <Authors>Hibara</Authors>
  <Copyright>Copyright © 2026 Hibara</Copyright>
  <Version>1.0.0</Version>
  <AssemblyVersion>1.0.0.0</AssemblyVersion>
  <FileVersion>1.0.0.0</FileVersion>
  <InformationalVersion>1.0.0</InformationalVersion>
</PropertyGroup>
```

| プロパティ | 用途 |
|---|---|
| `AssemblyName` | 出力される EXE / DLL のファイル名 |
| `Product` | 製品名。Windows のファイルプロパティ「製品名」に表示 |
| `AssemblyTitle` | Windows のファイルプロパティ「ファイルの説明」に表示 |
| `Description` | Windows のファイルプロパティ「コメント」に表示 |
| `Company` | 会社名。Windows のファイルプロパティ「会社」に表示 |
| `Copyright` | 著作権表記 |
| `AssemblyVersion` | ランタイムの参照解決に使われる厳密バージョン。メジャー変更時のみ更新 |
| `FileVersion` | ファイルバージョン。ビルドごとに更新してもランタイム影響なし |
| `InformationalVersion` | 表示用バージョン。SemVer preリリース や Git ハッシュを含められる |

#### 8.2.3 バージョン番号の運用ルール

- `Version` は SemVer (`MAJOR.MINOR.PATCH`) に従う
- `AssemblyVersion` は `1.0.0.0` のように固定し、**メジャーバージョン変更時のみ更新**する
- `FileVersion` と `InformationalVersion` は `Version` と同期させてよい
- About ダイアログに表示するバージョンは `InformationalVersion` を使用する

#### 8.2.4 コードからのアセンブリ情報取得

`System.Reflection` で属性を取得する。About ダイアログで使用する共通ユーティリティを用意する:

```csharp
using System.Reflection;

public static class AppInfo
{
    private static readonly Assembly ThisAssembly = Assembly.GetExecutingAssembly();

    public static string Product =>
        ThisAssembly.GetCustomAttribute<AssemblyProductAttribute>()?.Product ?? "";

    public static string Copyright =>
        ThisAssembly.GetCustomAttribute<AssemblyCopyrightAttribute>()?.Copyright ?? "";

    public static string Description =>
        ThisAssembly.GetCustomAttribute<AssemblyDescriptionAttribute>()?.Description ?? "";

    /// <summary>
    /// 表示用バージョン。InformationalVersion から "+" 以降を除去したもの。
    /// </summary>
    public static string DisplayVersion
    {
        get
        {
            var info = ThisAssembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                ?.InformationalVersion ?? "";
            var plusIndex = info.IndexOf('+');
            return plusIndex >= 0 ? info[..plusIndex] : info;
        }
    }

    /// <summary>
    /// About ダイアログで表示するアイコンの avares URI。OS により切り替える。
    /// Windows と macOS で見栄えの異なるアイコンを使い分けるため、XAML へ
    /// URI をハードコードせずこのプロパティ経由で取得すること。
    /// </summary>
    public static string AboutIconUri =>
        OperatingSystem.IsMacOS()
            ? "avares://ThunderbirdMsgFilterCopy/Assets/main-icon-mac_64x64.png"
            : "avares://ThunderbirdMsgFilterCopy/Assets/main-icon_64x64.png";
}
```

`InformationalVersion` はビルド環境によっては `1.0.0+<Gitハッシュ>` のような形になることがあるため、表示時は `'+'` 以降を除去する。

#### 8.2.5 About ダイアログ (ヘルプ → バージョン情報)

`5.2 メニュー構成` の「ヘルプ → バージョン情報」で表示するダイアログ。
レイアウトは縦方向中央揃えの以下の順序で構成する:

1. アプリケーションアイコン (64 × 64 PNG)。Windows では `main-icon_64x64.png`、macOS では `main-icon-mac_64x64.png` を使用する。実行時に `OperatingSystem.IsMacOS()` で切り替え、AXAML への URI ハードコードは禁止。`AppInfo.AboutIconUri` で URI を取得し、コードビハインドで `Bitmap` 化して `Image.Source` に代入する (8.2.4 / 8.2.6 参照)
2. 製品名 (`Product`、18pt Bold)
3. バージョン (`Version {DisplayVersion}`、13pt)
4. 著作権表記 (`Copyright`、12pt)
5. OK ボタン (80 幅、IsDefault / IsCancel 両 true)

ウィンドウサイズは固定 (約 340 × 320)、リサイズ不可、タスクバー非表示、
オーナーウィンドウ中央に表示 (`WindowStartupLocation="CenterOwner"`)。
ウィンドウタイトルは `{Product} について` とする。

テキスト値・アイコン URI はすべて `AppInfo` クラス経由で取得し、XAML にハードコードしない。

イメージサンプルは、`Asesets/sample_about.png`に配置してある。

#### 8.2.6 アセットファイルの配置と参照

アセットファイルは **アプリプロジェクト直下の `Assets/` フォルダ** に配置する。

| ファイル | 用途 |
|---|---|
| `Assets/main-icon.ico` | ウィンドウアイコン、EXE アイコン (Windows) |
| `Assets/main-icon.icns` | `.app` バンドルアイコン (macOS) |
| `Assets/main-icon_64x64.png` | About ダイアログ表示用 (Windows、64×64) |
| `Assets/main-icon-mac_64x64.png` | About ダイアログ表示用 (macOS、64×64) |

About ダイアログのアイコンは Windows / macOS でデザインを使い分ける。両ファイルとも 64×64 PNG とし、ファイル名のみで OS 用途を判別できるようにする。

`.csproj` に以下を記述し、`Assets/` 配下を Avalonia リソースとして埋め込む:

```xml
<ItemGroup>
  <AvaloniaResource Include="Assets\**" />
</ItemGroup>
```

`.ico` を EXE のアイコンとして焼き込む場合は `<PropertyGroup>` に
`<ApplicationIcon>Assets\main-icon.ico</ApplicationIcon>` を記述する。

XAML から参照する際は **`avares://` スキーム**を使用する。ただし About ダイアログのアイコンは OS により異なるため、XAML 側にハードコードせず、`AppInfo.AboutIconUri` 経由で URI を取得し、**コードビハインドで `Bitmap` を生成して `Image.Source` に代入する**。

XAML 側は `Image` に名前を付けるだけで `Source` を書かない:

```xml
<Image Name="AboutIconImage" Width="64" Height="64" HorizontalAlignment="Center"/>
```

コードビハインド (`AboutWindow.axaml.cs`) のコンストラクタで `InitializeComponent()` の後に:

```csharp
using var stream = AssetLoader.Open(new Uri(AppInfo.AboutIconUri));
AboutIconImage.Source = new Bitmap(stream);
```

`{x:Static local:AppInfo.AboutIconUri}` で直接バインドする方式は **使用しない**。`Image.Source` は `IImage` 型のため、XAML リテラル (`Source="avares://..."`) では XAML パーサが暗黙的に文字列→`Bitmap` 変換を行うが、`{x:Static}` は型変換パスを通らずに値をそのまま渡すため、`string` のまま `IImage` プロパティに代入されて型不適合エラーとなる (Avalonia 11/12 共通の挙動)。コードビハインドで明示的に `Bitmap` 化すれば、URI 文字列は `AppInfo.AboutIconUri` の一箇所に集約されたまま、AXAML への URI ハードコードも回避できる。

OS 非依存のリソース (例: ウィンドウ装飾用画像など、両 OS で同じものを使うアセット) を XAML から直接参照する場合は、従来通り `avares://` URI をリテラルで書いてよい。

ウィンドウアイコンの指定は相対パス形式 (`Icon="/Assets/main-icon.ico"`) でも可。

#### 8.2.7 macOS バンドル対応とリリース手順

macOS 向けには `.app` バンドル化した上で、コード署名・公証 (Notarization)・DMG パッケージ化を行う。これらは JetBrains Rider でリリースビルドした後、リポジトリルート直下のシェルスクリプト `build_and_release.sh` を手動実行する運用とする。

##### 8.2.7.1 基本方針

- 配布形態は **`.app` バンドル**を内包した **`.dmg`** とする (公証済み)
- コード署名は **Developer ID Application** 証明書を使用
- 公証 (Notarization) および Staple まで自動で完了させる
- v1 では `osx-arm64` (Apple Silicon) を主ターゲットとし、`osx-x64` (Intel) は必要になった時点で追加ビルド
- v1 の対応 macOS は **12.0 (Monterey) 以降**。これは .NET 10 のサポート最小バージョンに合わせる

##### 8.2.7.2 ディレクトリ / ファイル配置

リポジトリ全体は以下のレイアウトを正とする。`build_and_release.sh` は **リポジトリルート直下** (`.sln` と同じ階層) に配置すること。スクリプト内部でアプリプロジェクトディレクトリ (`ThunderbirdMsgFilterCopy/`) を `$PROJECT_DIR`、リポジトリルートを `$REPO_ROOT` として参照する。

```
ThunderbirdMsgFilterCopy/                        ← リポジトリルート (= REPO_ROOT)
├── ThunderbirdMsgFilterCopy.sln
├── build_and_release.sh                         ← ビルドスクリプト
│
├── dmg/                                         ← DMG 専用アセット (リポジトリルート直下、任意)
│   ├── volume.icns                              ← DMG マウント時のアイコン
│   └── dmg-background.png                       ← DMG ウィンドウ背景
│
├── ThunderbirdMsgFilterCopy/                    ← アプリプロジェクト (= PROJECT_DIR)
│   ├── ThunderbirdMsgFilterCopy.csproj
│   ├── Info.plist                               ← macOS バンドル用
│   ├── entitlements.plist                       ← 署名時の entitlements
│   │
│   └── Assets/                                  ← アプリ内アセット
│       ├── main-icon.ico                        ← Windows 用
│       ├── main-icon.icns                       ← macOS バンドルアイコン
│       ├── main-icon_64x64.png                  ← About ダイアログ用 (Windows)
│       └── main-icon-mac_64x64.png              ← About ダイアログ用 (macOS)
│
├── ThunderbirdMsgFilterCopy.Core/
└── ThunderbirdMsgFilterCopy.Tests/
```

| パス | 必須 | 用途 |
|---|:---:|---|
| `build_and_release.sh` | ● | ビルド・署名・公証・DMG 化を一括実行 |
| `ThunderbirdMsgFilterCopy/Info.plist` | ● | `.app` バンドルの `Contents/Info.plist` として使用 |
| `ThunderbirdMsgFilterCopy/entitlements.plist` | ● | コード署名時に埋め込む entitlements。無ければスクリプトが自動生成 |
| `ThunderbirdMsgFilterCopy/Assets/main-icon.icns` | ● | `.app` のアイコン。`Contents/Resources/main-icon.icns` にコピーされる |
| `ThunderbirdMsgFilterCopy/Assets/main-icon_64x64.png` | ● | About ダイアログ表示用 (Windows) |
| `ThunderbirdMsgFilterCopy/Assets/main-icon-mac_64x64.png` | ● | About ダイアログ表示用 (macOS) |
| `dmg/volume.icns` | ○ | DMG マウント時のボリュームアイコン (リポジトリルート直下) |
| `dmg/dmg-background.png` | ○ | DMG ウィンドウの背景画像 (リポジトリルート直下) |

役割を以下のように明確に分離する:

- **アプリ実行時にも必要なアセット** (バンドルアイコン、About 用 PNG など) → `ThunderbirdMsgFilterCopy/Assets/`
- **DMG ビルド時にしか使わないアセット** (DMG ボリュームアイコン、DMG ウィンドウ背景) → `<repo-root>/dmg/`

アプリアイコンは **`ThunderbirdMsgFilterCopy/Assets/main-icon.icns` のみを正とする**。リポジトリルート直下の `dmg/` には `.app` 用のアイコンを置かない。`build_and_release.sh` は DMG アセットを `$REPO_ROOT/dmg/` のみから探索し、アプリプロジェクト配下のフォールバック探索は行わない。

##### 8.2.7.3 Info.plist

`ThunderbirdMsgFilterCopy/Info.plist` に以下の内容を記述する。`CFBundleShortVersionString` と `CFBundleVersion` は**ビルド時に `.csproj` の `<Version>` で上書きされる**ため、初期値は固定文字列 (例: `1.0.0`) でよい。

```xml
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN"
  "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>CFBundleName</key>
    <string>ThunderbirdMsgFilterCopy</string>

    <key>CFBundleDisplayName</key>
    <string>ThunderbirdMsgFilterCopy</string>

    <key>CFBundleIdentifier</key>
    <string>com.hibara.thunderbirdmsgfiltercopy</string>

    <key>CFBundleExecutable</key>
    <string>ThunderbirdMsgFilterCopy</string>

    <key>CFBundlePackageType</key>
    <string>APPL</string>

    <key>CFBundleInfoDictionaryVersion</key>
    <string>6.0</string>

    <key>CFBundleShortVersionString</key>
    <string>1.0.0</string>

    <key>CFBundleVersion</key>
    <string>1.0.0</string>

    <key>CFBundleIconFile</key>
    <string>main-icon</string>

    <key>LSMinimumSystemVersion</key>
    <string>12.0</string>

    <key>LSApplicationCategoryType</key>
    <string>public.app-category.utilities</string>

    <key>NSHighResolutionCapable</key>
    <true/>

    <key>NSHumanReadableCopyright</key>
    <string>Copyright(C) 2026 HiBARA Software, LLC</string>
</dict>
</plist>
```

各キーの要点:

| キー | 値の決め方 |
|---|---|
| `CFBundleIdentifier` | 逆ドメイン形式で固定。公証・署名の識別子となるので途中で変更しないこと |
| `CFBundleExecutable` | `.csproj` の `<AssemblyName>` と**完全一致**させる (`Contents/MacOS/` 配下のバイナリ名そのまま) |
| `CFBundleIconFile` | `Contents/Resources/` 配下のファイル名。拡張子は省略可 |
| `LSMinimumSystemVersion` | .NET 10 の macOS 最小要件 `12.0` 以上を指定 |
| `NSHighResolutionCapable` | Retina 対応宣言。無いと拡大ボケ表示になる |

##### 8.2.7.4 entitlements.plist

`ThunderbirdMsgFilterCopy/entitlements.plist` を以下の内容で配置する。リポジトリに**明示的にコミットしておく** (`build_and_release.sh` は無ければ自動生成するが、再現性を確保するためにリポジトリで管理する)。

```xml
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN"
  "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>com.apple.security.cs.allow-jit</key>
    <true/>
    <key>com.apple.security.cs.allow-unsigned-executable-memory</key>
    <true/>
    <key>com.apple.security.cs.disable-library-validation</key>
    <true/>
</dict>
</plist>
```

各エントリは .NET / Avalonia アプリが hardened runtime 下で動作し、かつ公証を通過するための定番設定:

- `allow-jit`: .NET ランタイムの JIT コンパイルを許可
- `allow-unsigned-executable-memory`: .NET ランタイムの実行メモリ確保を許可
- `disable-library-validation`: Avalonia のネイティブ `.dylib` (`libAvaloniaNative.dylib` 等) のロードを許可

本アプリは AppleScript による他アプリ制御を行わないため、`com.apple.security.automation.apple-events` は**含めない**。

##### 8.2.7.5 アプリアイコンと DMG アセット

アプリアイコン `.icns` の生成は既存の `.icns` 生成スクリプト (別途保持) を流用する。`build_and_release.sh` はアイコン生成までは面倒を見ないので、**あらかじめ `ThunderbirdMsgFilterCopy/Assets/main-icon.icns` を用意しておく**こと。

DMG 用アセットは任意。**リポジトリルート直下の `dmg/`** に配置する (アプリプロジェクト配下ではない):

- `<repo-root>/dmg/volume.icns` を置くと DMG マウント時のアイコンが置き換わる
- `<repo-root>/dmg/dmg-background.png` を置くと DMG ウィンドウの背景画像として表示される (推奨サイズ: 660 × 400 px)

両方とも省略した場合、DMG は create-dmg の既定スタイルで生成される。

##### 8.2.7.6 ビルドスクリプト (build_and_release.sh)

スクリプトは 6 ステップ構成で以下を自動実行する:

1. `dotnet publish -c Release -r osx-arm64 --self-contained true` でビルド
2. `.app` バンドル構造の組み立て (`Contents/MacOS/`、`Contents/Resources/`、`Info.plist` 配置、アイコンコピー、`PkgInfo` 生成、バージョン上書き)
3. コード署名 (`.dylib` 個別 → 実行ファイル → バンドル全体の順、`--options runtime` + `--timestamp`)
4. 公証 (`xcrun notarytool submit --wait`) + Staple (最大 5 回リトライ)
5. Gatekeeper 検証 (`spctl --assess`)
6. DMG / ZIP の生成 (DMG も公証対象)

スクリプト冒頭の以下の変数のみ編集して使用する:

```bash
DEVELOPER_ID="Developer ID Application: Mitsuhiro Hibara (XXXXXXXXXX)"
APPLE_ID="m@hibara.org"
TEAM_ID="XXXXXXXXXX"
APP_PASSWORD="xxxx-xxxx-xxxx-xxxx"

APP_NAME="ThunderbirdMsgFilterCopy"
FRAMEWORK="net10.0"
ARCH="osx-arm64"

DMG_PREFIX="TbMsgFilterCopy"   # DMG ファイル名プレフィックス
BETA_SUFFIX="b"                # β版は "b"、正式版は ""
```

`APP_NAME` は **必ず `.csproj` の `<AssemblyName>` と一致**させること。一致しないとステップ 1 の出力検証で実行ファイルが見つからず停止する。

依存ツール (スクリプト実行前に Homebrew などで導入しておくこと):

- `dotnet` (.NET 10 SDK)
- `xcrun` (Xcode Command Line Tools)
- `create-dmg` (Homebrew: `brew install create-dmg`)
- `SetFile` (Xcode Command Line Tools に同梱。DMG のカスタムアイコン設定に使用)

主要な実行オプション:

```bash
./build_and_release.sh              # フルビルド + DMG 生成 (デフォルト)
./build_and_release.sh --skip-build # 既存ビルドを使って DMG だけ作り直す
./build_and_release.sh --skip-notarize # 公証スキップ (開発時のローカル確認用)
./build_and_release.sh --zip        # DMG に加えて ZIP も生成
./build_and_release.sh --verbose    # 詳細ログ表示
```

##### 8.2.7.7 バージョン管理と DMG ファイル名規則

**バージョン文字列の真実の源泉は `.csproj` の `<Version>`** とする。`build_and_release.sh` はビルド時に以下を行うことで、Info.plist のバージョンを自動的に `.csproj` と同期させる:

```bash
# .csproj から <Version> を抽出
CSPROJ_VERSION=$(sed -n 's/.*<Version>\(.*\)<\/Version>.*/\1/p' "$CSPROJ_FILE")
# Info.plist のバージョン 2 キーを上書き
/usr/libexec/PlistBuddy -c "Set :CFBundleShortVersionString $CSPROJ_VERSION" Info.plist
/usr/libexec/PlistBuddy -c "Set :CFBundleVersion $CSPROJ_VERSION" Info.plist
```

したがって、バージョン更新時は **`.csproj` の `<Version>` (および `<FileVersion>` / `<InformationalVersion>`) のみを変更**し、Info.plist は触らない。

DMG ファイル名は以下の規則で組み立てられる:

```
<DMG_PREFIX><VERSION_SHORT><BETA_SUFFIX>-<ARCH_SHORT>.dmg
```

- `VERSION_SHORT`: `.csproj` の `<Version>` からドットを除去したもの (`1.0.0` → `100`)
- `ARCH_SHORT`: `osx-arm64` → `arm64`、`osx-x64` → `x64`

例:

- `TbMsgFilterCopy100b-arm64.dmg` (β版 Apple Silicon)
- `TbMsgFilterCopy100-arm64.dmg` (正式版 Apple Silicon)
- `TbMsgFilterCopy100-x64.dmg` (正式版 Intel)

##### 8.2.7.8 署名・公証の前提

事前準備として以下が必要。これらは開発者個人の資格情報のため、スクリプトには**直接埋め込まずローカルでのみ管理**する (公開リポジトリへコミットしない):

1. Apple Developer Program への登録
2. Developer ID Application 証明書のキーチェーンインポート
3. Apple ID 用 App 固有パスワードの発行 (<https://appleid.apple.com>)
4. Team ID の確認 (Apple Developer ポータル > Membership)

スクリプトは `xcrun notarytool` (Xcode 13 以降) を使用する。古い `altool` 方式には対応しない。

将来的に CI から実行する場合は、`DEVELOPER_ID` / `APPLE_ID` / `TEAM_ID` / `APP_PASSWORD` を環境変数 / GitHub Actions Secrets から読むように書き換える余地を残しておく (v1 ではローカル手動実行のみ)。

### 8.3 多言語対応 (ローカライゼーション)

#### 8.3.1 基本方針

- **既定 (ニュートラル) 言語は英語**とする
- 日本語環境でのみ日本語リソースを使用する
- 日英以外の言語環境 (ドイツ語、フランス語、中国語など) では**自動的に英語にフォールバック**する
- これは開発者の従来からの方針であり、日英以外のユーザーにも最低限英語で使える状態を保証するためである

#### 8.3.2 リソースファイル構成

.resx ファイルは以下のファイル名規則に従う:

ThunderbirdMsgFilterCopy/Resources/Strings.resx       ← 英語 (既定・ニュートラル)  
ThunderbirdMsgFilterCopy/Resources/Strings.ja.resx    ← 日本語 (サテライト)

**重要**: サフィックスなしの `Strings.resx` は .NET の `ResourceManager` において
「既定 (ニュートラル) リソース」として扱われる。日本語を `Strings.resx` に入れ、
英語を `Strings.en.resx` に分離する構成にすると、日英以外の言語環境で
意図せず日本語が表示されるため誤り。

#### 8.3.3 .csproj 設定

既定言語を明示するため、以下のプロパティを追加する:

```xml
<PropertyGroup>
  <NeutralLanguage>en</NeutralLanguage>
</PropertyGroup>
```

これによりサテライトアセンブリ生成時の最適化が有効になる。

#### 8.3.4 文字列リソースの利用

UI に表示するすべての文字列は `.resx` から取得する。XAML へのハードコードは禁止。

- XAML からの参照: `{x:Static resources:Strings.MyKey}` などの形式
  (具体的な書き方は Avalonia での .resx 参照方法に従う)
- コードビハインドからの参照: 自動生成される `Strings.Designer.cs` 経由

エラーメッセージ、ダイアログ文言、ボタンラベル、メニュー項目、ツールチップなど、
ユーザーに見えるすべての文言をリソース化すること。

例外:
- 製品名 (`ThunderbirdMsgFilterCopy`)、バージョン番号などの固有名詞は除く
- ログや例外の `Exception.Message` など、ユーザーに直接見せない内部文字列は除く

#### 8.3.5 リソースキーの命名規則

- 画面単位でプレフィックスを付ける: `Export_SelectAllButton`、`Import_DropZoneText` など
- エラーメッセージは `Error_` プレフィックス: `Error_ProfileNotFound` など
- ダイアログは `Dialog_` プレフィックス: `Dialog_ConfirmImport_Title` など
- 汎用ボタンなど共通文言は `Common_`: `Common_Ok`、`Common_Cancel` など

#### 8.3.6 対応言語の将来拡張

v1 で対応する言語は英語・日本語のみ。将来的に他言語を追加する場合は
`Strings.<cultureCode>.resx` を追加するだけで対応可能な設計とする。

#### 8.3.7 コマンドライン引数による言語上書き

スクリーンショット撮影や動作確認のため、起動時の引数で UI 言語を強制指定できるようにする。

##### 8.3.7.1 引数仕様

- `--lang <code>`: 表示言語を強制指定する
  - `<code>` が `ja` (大文字小文字区別なし) の場合、UI を日本語 (`ja-JP`) にする
  - `<code>` が `en` (大文字小文字区別なし) の場合、UI を英語 (`en-US`) にする
  - **それ以外の値が渡された場合は英語 (`en-US`) にフォールバックする**。エラー停止はしない
- `--lang=<code>` の形式 (`=` で連結) も受け付ける
- 引数を渡さなかった場合、または `--lang` の値が省略された場合は、従来どおりの挙動 (8.3.1 参照: OS 文化に従い、日本語環境なら日本語、それ以外は英語フォールバック)
- **エイリアス (`--ja` / `--en` 等) は提供しない**。将来的な言語追加時の一貫性確保のため、`--lang <code>` 形式に統一する
- 不明なその他の引数は無視する

例:

```
ThunderbirdMsgFilterCopy.exe --lang ja              # Windows
open ThunderbirdMsgFilterCopy.app --args --lang en  # macOS
ThunderbirdMsgFilterCopy.exe --lang fr              # → en にフォールバック (英語表示)
```

##### 8.3.7.2 適用方法

`Program.Main` の冒頭、Avalonia アプリの初期化 (`BuildAvaloniaApp().StartWithClassicDesktopLifetime(args)`) を呼ぶ前に以下を行う。

1. `args` を走査して `--lang` の値を取り出す
2. `ja` であれば `CultureInfo("ja-JP")` を、それ以外 (`en` / 不明値の両方) であれば `CultureInfo("en-US")` を生成
3. `CultureInfo.DefaultThreadCurrentUICulture` および `CultureInfo.DefaultThreadCurrentCulture` に代入
4. 引数に `--lang` 自体が含まれない場合は何もしない (OS 文化に従う既定挙動を維持)

参考実装:

```csharp
using System.Globalization;

public static int Main(string[] args)
{
    ApplyLanguageOverride(args);
    return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
}

private static void ApplyLanguageOverride(string[] args)
{
    var lang = ParseLangArg(args);
    if (lang is null) return;

    // ja のときのみ日本語、それ以外 (en / 不明値) は英語にフォールバック
    var culture = lang.Trim().Equals("ja", StringComparison.OrdinalIgnoreCase)
        ? new CultureInfo("ja-JP")
        : new CultureInfo("en-US");

    CultureInfo.DefaultThreadCurrentUICulture = culture;
    CultureInfo.DefaultThreadCurrentCulture   = culture;
}

private static string? ParseLangArg(string[] args)
{
    for (int i = 0; i < args.Length; i++)
    {
        var a = args[i];
        if (a.StartsWith("--lang=", StringComparison.OrdinalIgnoreCase))
            return a["--lang=".Length..];
        if (a.Equals("--lang", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            return args[i + 1];
    }
    return null;
}
```

##### 8.3.7.3 適用範囲と制約

- 言語切り替えはアプリ起動時の **1 回のみ**有効。実行中に動的に切り替える機能は v1 では実装しない
- 引数で上書きされた言語設定は、メニューラベル、ダイアログ、エラーメッセージなど、`.resx` から取得するすべての文字列に反映される
- 引数解析で例外を投げないこと。不正値は黙って英語にフォールバック (6.1 のロギング方針に従い、ログ出力もしない)

##### 8.3.7.4 位置付け

本機能はスクリーンショット撮影や開発時の動作確認を目的とした **補助機能**である。エンドユーザー向けの UI からは到達させない (メニューやヘルプから引数を案内しない)。README にコマンド例を載せる程度にとどめる。


## 9. v1 スコープ外 (将来拡張候補)

以下は **v1 では実装しない**。要望が出た場合に別バージョンで検討する。

- フィルターのマージモード (現状は上書きのみ)
- Thunderbird の自動終了機能
- 手動アカウントマッピング UI
- ホスト名部分一致によるマッチング
- Linux 対応
- `msgFilterRules.dat` のフルパーサ
- フィルター内容プレビュー
- 移動先フォルダの存在チェック (Local Folders 含む)
- Local Folders フィルターの `mailbox://` 参照ポータビリティ警告
- バックアップ世代管理 (現状は直前 1 件のみ)
- 過去のインポート履歴からの選択的復元
- ログファイル出力
- 多言語対応 (英語 UI 等)
- ポータブル版 Thunderbird 対応

## 10. 決定事項サマリ (対話ログから)

以下は仕様確定に至るまでに議論され、明示的に決定された事項の要約。実装中に迷った際はここに戻ること。

1. **Export の粒度**: 選択式、初期状態は全未選択、`[全選択]` ボタンあり
2. **マージ**: 実装しない。上書きのみ
3. **Linux 対応**: 実装しない
4. **Thunderbird 起動中の挙動**: 操作全般を無効化、自動終了は行わない
5. **ドロップ対象**: `.tbfilters` ファイルのみ。複数ドロップは 1 件目のみ処理
6. **バージョン非互換時**: 警告表示のみ、実行は可能
7. **マッチングロジック**: `serverFolderName` + `accountType` の完全一致のみ
8. **バックアップ**: アプリ専用ディレクトリに直前 1 セッションのみ保持
9. **復旧導線**: メニューの「直前のインポートを取り消す」のみ。結果ダイアログには復旧ボタンを置かない
10. **ロギング**: ファイル出力なし。エラーはダイアログのみ
11. **ツール名**: `ThunderbirdMsgFilterCopy`
12. **プロファイル推定**: スコアリング方式 (3.1.2 参照)
13. **prefs.js パース**: 正規表現で十分。失敗時はフォールバック
14. **Local Folders の扱い**: 他のアカウントと同じく通常処理。特別扱いしない
15. **開発 IDE**: Windows = Visual Studio 2022/2025/2026、macOS = JetBrains Rider。SDK スタイルプロジェクトで両対応 (1.2 参照)
16. **件数表示の文言**: 「N 件」単位のみの表現は禁止。必ず「N アカウント分のメッセージフィルター」のように対象を名詞で明示する (3.5.4 / 3.6.7 参照)
17. **チェックリストの操作性**: 行全体をクリック領域とし、チェックボックス部分だけを反応させる実装は不可 (5.6 参照)
18. **macOS 配布形態**: 公証済み `.app` を内包した `.dmg`。リポジトリルートの `build_and_release.sh` で Rider のリリースビルド後に手動実行 (8.2.7 参照)
19. **macOS 最小サポートバージョン**: 12.0 (Monterey)。.NET 10 の最小要件に追従
20. **バージョン管理の真実の源泉**: `.csproj` の `<Version>`。Info.plist はビルド時に自動同期される (8.2.7.7 参照)
21. **DMG 専用アセットの配置**: リポジトリルート直下の `dmg/` に置く。アプリプロジェクト (`ThunderbirdMsgFilterCopy/`) 配下には置かない。実行時に不要なアセットをアプリプロジェクトに混入させないため (8.2.7.2 / 8.2.7.5 参照)
22. **About ダイアログのアイコン**: Windows / macOS で別画像を使用する。Windows は `Assets/main-icon_64x64.png`、macOS は `Assets/main-icon-mac_64x64.png`。`AppInfo.AboutIconUri` で URI を取得し、コードビハインドで `Bitmap` 化して `Image.Source` に代入する。`{x:Static}` で直接バインドする方式は型変換が効かず使えない (8.2.4 / 8.2.5 / 8.2.6 参照)
23. **エクスポート画面の件数表示**: インポート完了時 / 取り消し完了時 / プロファイル切り替え時には、アカウント一覧を再走査してフィルター件数を更新する。アプリ再起動と同等の表示状態に揃うこと (3.5.5 / 3.6.6 / 3.7.5 参照)
24. **「直前のインポートを取り消す」メニューの有効化**: インポート完了直後はバックアップが新規作成されているので、メニューを必ず有効化する。取り消し完了時は無効化する。`HasBackupSession` をその両タイミングで再評価すること。アプリ再起動と同じ状態に揃うこと (3.7.6 / 5.5 参照)
25. **ダイアログのボタン配置**: 「キャンセル / 否定が左、肯定 / デフォルトが右」で OS 共通に統一する。Windows と macOS で配置を出し分けない。macOS の HIG (肯定が右) に合わせ、近年の Microsoft 製アプリや主要 Web フレームワークも同方向であるため両 OS の利用者にとって違和感が小さく、両 OS 併用ユーザーの混乱も避けられる。Esc は常にキャンセル (`IsCancel`)、Enter は常に肯定ボタン (`IsDefault`)。ボタンラベルは「はい/いいえ」より動詞 (「実行する」「取り消す」等) を優先する (5.7 / 3.6.5 / 3.7.4 参照)
26. **コマンドライン引数による言語上書き**: `--lang ja` / `--lang en` (`--lang=ja` 形式も可) で UI 言語を強制指定できる。スクリーンショット撮影や動作確認用の補助機能。`Program.Main` 冒頭で `CultureInfo.DefaultThreadCurrentUICulture` に代入することで `.resx` のフォールバック機構をそのまま活用する。`ja` 以外の値 (不明値含む) は英語にフォールバック。引数なしのときは従来通り OS 文化に従う。エイリアス (`--ja` / `--en` 等) は将来の言語追加時の一貫性のため提供しない (8.3.7 参照)
27. **プロファイルドロップダウンの「フィルター: N件」の意味**: `msgFilterRules.dat` のファイル数ではなく、プロファイル配下の全 `msgFilterRules.dat` に含まれる個別フィルターの合計本数 (各ファイル内の `name="..."` 出現回数の総和) とする。エクスポート画面のアカウント一覧の各行 `(フィルター: N件)` の総和とプロファイル側の数値が必ず一致するように揃える。同じ「フィルター」というラベルで違うものを数える状態を解消するための修正 (3.1.5 参照)
28. **確認・通知系ダイアログのウィンドウサイズ**: 幅 460px 固定、高さは `SizeToContent="Height"` で自動、`CanResize="False"` で統一する。`SizeToContent="WidthAndHeight"` だけだと Windows 環境でタイトルバー右側のウィンドウコントロール (約 130px) に押し負けて、`ThunderbirdMsgFilterCopy` のような長めのタイトルが「Th...」程度まで切り詰められるため。About ダイアログ (8.2.5) は対象外で従来どおり 340 × 320。エラー詳細などで本文が長くなる場合は `TextWrapping="Wrap"` や `ScrollViewer` で高さ方向に逃がし、幅は変えない (5.8 参照)


---

以上。
