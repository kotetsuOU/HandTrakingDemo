# ハプティクスアルゴリズム比較: Native C++ vs Pure C#

> 📂 **親ノード**: [Haptics.md](./Haptics.md) | 🏷️ **種類**: 🔬 アルゴリズム比較  
> [RealTimeOcclusion Wiki (ポータル)](./Wiki.md) に戻る

本ドキュメントでは、元の `com.shinolab.midair-haptics-unity-core` パッケージに実装されていたハプティクス（超音波提示）生成アルゴリズムと、本プロジェクト向けに完全 C# で再設計された `HAP_AUTDController` および `HAP_HapticsSources` のアルゴリズムの違い、およびその設計思想について解説します。

---

## 1. 概要と全体アーキテクチャの変更点

### 元パッケージ (Native C++)
* **動作**: `HapticsCentroidSource` や `HapticsEllipseSource` などのロジックがネイティブ C++ 側に隠蔽されており、Unity 側からは設定を流し込むだけのブラックボックスでした。また、STM (Spatio-Temporal Modulation) の生成やサンプリング処理が、複数の C++ スレッド上で複雑に並行動作していました。
* **課題**: Unity エディター上で「どのような STM 波形が出力されているか」をデバッグ・可視化することが困難でした。また、ネイティブプラグインのクラシャが Unity 全体を巻き込んで落ちるリスクがありました。

### 本システム (Pure C# / AUTD3Sharp)
* **動作**: すべての触覚形状生成アルゴリズム（Ellipse の PCA 計算、Random のサンプリング、STM フレームの合成）を、公式の C# ラッパーである `AUTD3Sharp` 上に構築された Pure C# スクリプトに移植・公開しました。
* **設計思想**: **「Unity エンジニアが触覚デザインを直接カスタマイズ・デバッグできること」** と **「Unity メインスレッドの安定動作」** を最優先としました。複雑なネイティブスレッドを廃止し、Unity のライフサイクル (`Update`) に完全に同期する軽量な設計へと変更しています。

---

## 2. 設計思想・アルゴリズムの比較

### 🔴 割愛・削除した点 (Omitted)
1. **CPUでの重いランダムポイント生成**: メインスレッドのスパイクを防ぐため、GPU コンピュートシェーダー (`Reservoir Sampling`) に完全移行。
2. **ブラックボックス化されたネイティブマルチスレッド (`HoloNativeBackend`)**: 全データを GPU 側で完了させ C# で同期的処理を行う設計に変更。
3. **GainStm および Virtual Aperture 機能**: 不要な高負荷演算を削ぎ落とし、軽量な `Sequential` と `FociStm` に絞り込み。

### 🟡 C#へ移植・再構築した点 (Ported & Modified)
1. **Ellipse Source の形状計算 (PCA)**: C# 側で GPU から受け取った「共分散行列 (Covariance Matrix)」をもとに固有値問題を解く実装に変更。
2. **Sequential モードと FociStm モードの C# 実装**: 複数接触時も $O(1)$ の極小計算量でハプティクスを提示できるよう最適化。
3. **速度ベースのダイナミックスケーリング (Velocity Scaling)**: 速度 (m/s) に応じて STM の周期や音圧振幅を動的調整。
4. **Modulation（変調）の競合解決 (`Frame Policy Override`)**: `ModulationOverride` クラスとして整理し自動適用。

### 🟢 追加した点 (Added)
1. **完全な Gizmo 可視化**: 焦点ポイント、楕円軌跡、16 点ランダムサンプル等の Scene ビュー可視化。

---

## 3. 数理モデル・要約

元のネイティブパッケージはブラックボックスであり、計算の最適化や Unity らしい調整が困難でした。

本システムでは、複雑な点群処理やサンプリングを **GPU 側（[Collision.md](./Collision.md) 参照）に完全にオフロード** することで CPU の余力を確保しました。そして空いた CPU (C#) 側には、`Sequential` 出力モードや速度ベースの変調、優先度解決といった **「より表現力豊かで、ゲームエンジンに適したリアルタイム触覚デザインロジック」** を追加実装しています。

---

## 4. 仕様・パラメータ詳細

具体的な実装スクリプトおよび操作方法については [Haptics.md](./Haptics.md) および [HowToUseHaptics.md](./HowToUseHaptics.md) を参照してください。

---

## 5. デバッグ・留意事項

* 触覚デザインのデバッグ時は `HAP_GizmoVisualizer` を使用し、意図した座標に焦点が生成されているかを Scene ビューで確認できます。
