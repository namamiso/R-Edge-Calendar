# AGENTS.md — Win11 Right-Edge Calendar (WPF/.NET 8)

## 0. 目的（絶対に守る）
Windows 11 常駐の軽量ネイティブアプリを作る。

- 右端(WorkingArea) 2〜6px にカーソルが入り、100ms滞在で右側パネルをフェードイン表示
- パネルはフォーカスを奪わない（NoActivate）
- Googleカレンダーと同期し、パネル内で予定の追加・編集・削除（CRUD）を完結
- WebView2 / Electron 禁止。軽量優先

## 1. スコープ（MVP）
### Must
- アカウント: 単一Googleアカウント
- カレンダー: 複数カレンダー統合表示、カレンダー単位でON/OFF
- 同期範囲: 今日を基準に「過去31日〜未来31日」（計62日）
- イベント: 単発のみ（繰り返しはMVP外）
- 終日予定: 対応
- タイムゾーン: 表示はローカル、保存はAPI仕様に沿う
- トリガー: 右端ホバー + グローバルホットキー（既定 Win+Alt+C）
- フルスクリーン抑制: 前面がフルスクリーンならホバー表示しない（ホットキーは許可）

### Must NOT（MVPでは実装しない）
- 参加者（attendees）編集、招待、RSVP
- 通知/リマインダー（将来追加しやすい設計にする）
- 繰り返し（RRULE）展開、例外日のUI

## 2. 非機能（軽量の掟）
- UIスレッドをI/Oで止めない（ネットワーク/DBは常にasync）
- 常駐アイドル時のCPUは極小
  - 通常ポーリング: 80〜120ms
  - 右端近傍時のみ: 16ms程度でdwell精度を確保
- dwell成立後の表示は体感即時（目安 250ms以内、フェード込み）
- 秘密情報をログに出さない（トークン、Auth code、HTTPヘッダ等）

## 3. アーキテクチャ（簡素に、堅牢に）
- UI: WPF + .NET 8
- 依存は最小。重いMVVMフレームワークは使わない（INotifyPropertyChangedを自前で可）
- DB: SQLite（Microsoft.Data.Sqlite）
- Google同期: Calendar API (REST) を HttpClient + System.Text.Json で呼ぶ
- 認証: OAuth 2.0 Authorization Code + PKCE（システムブラウザ + ループバック）
- トークン保存: Windows資格情報マネージャ（DPAPI）

推奨プロジェクト:
- src/EdgeCalendar.App (WPF)
- src/EdgeCalendar.Core (Domain + interfaces)
- src/EdgeCalendar.Infrastructure (SQLite + Google API + Auth)
- src/EdgeCalendar.Tests (任意)

## 4. UI挙動ルール
- PanelWindowはNoActivate。表示しても入力フォーカスを奪わない
- マウスアウトは猶予250msで隠す（チラつき防止）
- 表示位置は「カーソルがいるモニタ」のWorkingArea右端にドック
- フルスクリーン中のホバー表示は禁止（ただしホットキーは許可）

## 5. 同期と競合（ブレ禁止）
- オフライン編集はしない（ネットワーク必須）
- 競合はサーバー優先
  - If-Match（ETag）で条件付き更新
  - 競合発生時は勝手に上書きしない
  - ローカル編集案はConflictLogにJSONで退避し、UIに通知

## 6. 受け入れ条件（MVP DoD）
1) 右端2〜6px + 100ms滞在でフェードイン表示
2) NoActivateでフォーカスを奪わない
3) フルスクリーン前面時はホバー表示しない
4) Win+Alt+Cでトグル
5) Google認証が通り、トークンが安全に保存される
6) 複数カレンダー統合表示 + ON/OFF
7) 同期窓は前後31日で固定
8) CRUDがパネル内で完結し、Googleに反映
9) 競合時に上書きせず、退避ログが残る
10) アイデル時が軽い（ポーリング間引き）

## 7. Codex実行方針
- 変更は小さく。1回のタスクで1レイヤー（Shell / UI / Sync）だけ触る
- コマンド実行は必要最小限（dotnet build/test中心）
- 外部インストールやシステム設定変更は事前相談
