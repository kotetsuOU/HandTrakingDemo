# RealTimeOcclusion PCV デバッグビューア 設計思想・関数仕様ドキュメント

本ドキュメントは、点群（Point Cloud）データのリアルタイム可視化と確認をサポートする「デバッグビューア (PointCloudViewer)」の設計思想、各モジュールの役割、およびファイル構成をまとめたテクニカルリファレンスです。

---

## 🔗 統合プロジェクトポータル

本システムは、プロジェクトのメインポータルである **[RealTimeOcclusion システム統合 Wiki (WIKI.md)](./WIKI.md)** の「デバッグ表示ノード」として位置づけられています。

---

## 1. デバッグビューアシステム概要

本システムは、三次元点群空間を Unity シーンビューおよびゲームビューで素早くプレビューし、位置合わせ（キャリブレーション）や色・サイズのビジュアル確認を行うための基盤です。
不要な空間検索やフィルタリングの機能を削減し、シンプルかつ軽量なファイルのロードと描画に特化しています。

```
[Unity シーンビュー / インスペクタ]
               │
               ▼
       [PCV_Controller] (キャリブレーション姿勢補正)
        /            \
       /              \
      ▼                ▼
[PCV_DataManager]   [PCV_Renderer]
(データロード・保持)  (点群メッシュ描画)
```

### 提供価値
*   **軽量なファイルロードと可視化**: 
    外部の PLY / TXT 形式の点群データを CPU で高速にロードし、即座に Unity 上で Mesh としてプレビューします。
*   **直感的なキャリブレーション（姿勢補正）**: 
    インスペクターから対象オブジェクトへの Transform （位置・回転）を動的に適用し、実世界のデバイス（カメラ等）と仮想空間のアライメントを容易にします。
*   **レンダリングソースの動的切り替え**: 
    PCV ファイル（CPU）と RealSense 統合点群（GPU Global Buffer）の描画ソースを瞬時に切り替え、PCDRendererFeature にデータを供給します。

---

## 2. ファイル構成・ツリー構造

`Assets/Scripts/Debug/PointCloudViewer` 以下のスクリプト構成です。

<details open><summary>├── <b>[PointCloudViewer]</b></summary>
    
    ├── [PCV_Controller.cs](./Assets/Scripts/Debug/PointCloudViewer/PCV_Controller.cs) — PCV デバッグシステム全体の司令塔。レンダリングソースの切り替えや姿勢補正 (ApplyTransformCorrection) を担当。
    ├── [PCV_Settings.cs](./Assets/Scripts/Debug/PointCloudViewer/PCV_Settings.cs) — インスペクターのパラメータ設定、プロファイル管理のデータコンテナ。
    ├── [PCV_ConfigIO.cs](./Assets/Scripts/Debug/PointCloudViewer/PCV_ConfigIO.cs) — JSON ファイルからの PCV プロファイルの保存と読み込み。
    ├── [PCV_DataManager.cs](./Assets/Scripts/Debug/PointCloudViewer/PCV_DataManager.cs) — 点群データの保持とデータ更新のイベント通知 (OnDataUpdated)。
    ├── [PCV_Data.cs](./Assets/Scripts/Debug/PointCloudViewer/PCV_Data.cs) — 点群データ (頂点、色) のメモリ内保持クラス。
    ├── [PCV_Loader.cs](./Assets/Scripts/Debug/PointCloudViewer/PCV_Loader.cs) — PLY や TXT ファイルからの点群パースとマルチスレッドロード処理。
    ├── [PCV_Renderer.cs](./Assets/Scripts/Debug/PointCloudViewer/PCV_Renderer.cs) — PCV_MeshGenerator を用いて点群を Unity Mesh としてシーンに描画。
    └── [PCV_MeshGenerator.cs](./Assets/Scripts/Debug/PointCloudViewer/PCV_MeshGenerator.cs) — 点群座標とカラー配列から Unity Mesh オブジェクト (Topology: Points) を生成。

</details>

---

## 3. 主要モジュールの設計思想

### 1. `PCV_Controller`
*   **設計思想**: システム全体の司令塔として、インスペクターの状態変更を監視し、設定ファイルの変更や処理の切り替えをハンドリングします。
*   **座標補正機能（ApplyTransformCorrection）**:
    Unity 上でのキャリブレーション時に、Viewer 自身の Transform 行列を計算し、登録されている全オブジェクト（カメラの Transform など）に適用して座標系を同期・リセットする位置補正機能を備えています。

### 2. `PCV_DataManager` & `PCV_Loader`
*   **設計思想**: CPU での点群パースとメモリ保持を行います。
*   ファイルパスが指定されると、`PCV_Loader` が効率的にテキストやバイナリデータを解析し、`PCV_Data` クラスに頂点情報を格納します。格納完了後、`PCV_DataManager` が `OnDataUpdated` イベントを発行し、即座にレンダラーへデータが同期されます。

### 3. `PCV_Renderer` & `PCDRendererFeature` の連携
*   **設計思想**: デバッグ用の点群を URP のレンダリングパイプラインに供給します。
*   シーンビューで確認するための Mesh 生成（`PCV_MeshGenerator`）と並行して、URP 用の `PCDRendererFeature` に対して `SetPointCloudData(data)` もしくは `SetUseGlobalBuffer(true)` を呼び出し、オクルージョンパイプラインへ正しいソースをルーティングします。
