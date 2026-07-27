# PCV デバッグビューア (PointCloudViewer) 仕様書

> 📂 **親ノード**: [Wiki.md](./Wiki.md) | 🏷️ **種類**: 🏗️ システム設計書  
> [RealTimeOcclusion Wiki (ポータル)](./Wiki.md) に戻る

本ドキュメントは、点群（Point Cloud）データのリアルタイム可視化と確認をサポートする「デバッグビューア (PointCloudViewer)」の設計思想、各モジュールの役割、およびファイル構成をまとめたテクニカルリファレンスです。

---

## 1. 概要

本システムは、三次元点群空間を Unity シーンビューおよびゲームビューで素早くプレビューし、位置合わせ（キャリブレーション）や色・サイズのビジュアル確認を行うためのデバッグ基盤です。

不要な空間検索やフィルタリングの機能を削減し、シンプルかつ軽量なファイルのロードと描画に特化しています。

---

## 2. 設計思想・アーキテクチャ

### 2.1 コンポーネント構造とデータフロー

```text
[Unity シーンビュー / インスペクター]
               │
               ▼
       [PCV_Controller] (キャリブレーション姿勢補正)
        /            \
       /              \
      ▼                ▼
[PCV_DataManager]   [PCV_Renderer]
(データロード・保持)  (点群メッシュ描画)
```

### 2.2 ファイル構成・ツリー構造

`Assets/Features/Debug/Scripts/PointCloudViewer/` 配下のスクリプト構成です。

* `PCV_Controller.cs`: PCV デバッグシステム全体の司令塔。レンダリングソースの切り替えや姿勢補正 (`ApplyTransformCorrection`) を担当。
* `PCV_Settings.cs`: インスペクターのパラメータ設定、プロファイル管理のデータコンテナ。
* `PCV_ConfigIO.cs`: JSON ファイルからの PCV プロファイルの保存と読み込み。
* `PCV_DataManager.cs`: 点群データの保持とデータ更新のイベント通知 (`OnDataUpdated`)。
* `PCV_Data.cs`: 点群データ（頂点、色）のメモリ内保持クラス。
* `PCV_Loader.cs`: PLY や TXT ファイルからの点群パースとマルチスレッドロード処理。
* `PCV_Renderer.cs`: `PCV_MeshGenerator` を用いて点群を Unity Mesh としてシーンに描画。
* `PCV_MeshGenerator.cs`: 点群座標とカラー配列から Unity Mesh オブジェクト (`Topology: Points`) を生成。

---

## 3. セットアップ・使用方法

1. デバッグしたいシーンオブジェクトに `PCV_Controller` をアタッチします。
2. インスペクターで読み込みたい PLY / TXT 点群ファイルのパスを指定します。
3. `PCV_Controller` の **Apply Transform Correction** ボタンで姿勢アライメントを同期補正します。
4. 必要に応じて `PCV_Renderer` または `PCDRendererFeature` にデータソースを引き渡します。

---

## 4. 仕様・パラメータ詳細

### 4.1 主要モジュールの役割

* `PCV_Controller`: 設定ファイルの変更検知、姿勢補正 (`ApplyTransformCorrection`)、描画ソース切り替え。
* `PCV_DataManager` & `PCV_Loader`: PLY/TXT の非同期ロードと `PCV_Data` への格納、`OnDataUpdated` イベント発火。
* `PCV_Renderer`: メッシュ生成 (`PCV_MeshGenerator`) および URP 用 `PCDRendererFeature` への点群バッファ供給 (`SetPointCloudData` / `SetUseGlobalBuffer`)。

---

## 5. デバッグ・留意事項

* PCV ファイルの描画と RealSense カメラのリアルタイム点群描画ソースは `PCV_Controller` 上でワンタップ切り替え可能です。
* 大容量の PLY ファイルを読み込む際は、非同期ロード処理中にメモリ割り当てスパイクが発生しないよう事前に頂点数をリサンプリングすることを推奨します。
