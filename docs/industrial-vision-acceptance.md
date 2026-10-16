# JLVision 工业视觉离线验收标准（第一版）

本方案只对**离线 Shape/NCC 匹配**进行可重复的质量评估，不模拟真实工业相机、机器人或 PLC。Windows x64 下使用 `tools/JLVision.DatasetBenchmark`，运行结果为 JSON。

## 1. 验收数据

- 训练集与测试集必须分离，避免用同一帧“训练后验证自己”导致虚高。
- 数据集需要记录工件型号、物理位置、光照、旋转、焦距/曝光、模糊、遮挡、缺件等条件。**正样本和负样本都要有**；计数不足时应设置 `minPositiveFrames` / `minNegativeFrames` 使质量门禁失败。
- 正样本的 `expectedPresent=true`，可同时提供 `expectedRow/expectedColumn`（像素）、`expectedXmm/expectedYmm`（物理空间毫米）和 `expectedAngleRad`（弧度）。
- 标注位置必须是**算法模型参考点**，不能随意将检测矩形中心替代为模型参考点。毫米真值应由独立测量得到，不能用同一套待验算法结果反向“标注”。
- 用 `repeatGroup` 将**工件不移动、相机和光源固定**下的多次独立采集分组；有位移或重新装夹的帧不能放入同一重复性组。
- 不要把含客户产品内容的原图、标注或路径提交到公共仓库；CI 使用合成数据，真实数据由客户本地执行。

## 2. 平面仿射标定和误差定义

仅适用于相机姿态固定、已完成像素到世界坐标标定且平面近似满足仿射的场景：

```text
X_mm = xFromColumn * column + xFromRow * row + xOffset
Y_mm = yFromColumn * column + yFromRow * row + yOffset
```

坐标输入为图像 `row, column`；世界输出为 `X,Y`（mm）。矩阵系数必须由**独立标定**获得。两方向应无奇异、系数有限；仅填示例矩阵绝不能证明标定有效。

- **单帧位置误差：** `sqrt((X-X_true)^2 + (Y-Y_true)^2)`，单位 mm，取每帧误差的最大值与 `maxPositionErrorMm` 比较。
- **角度误差：** 将预测与真值差值用 `atan2(sin(Δ),cos(Δ))` 归一到 ±π，再取绝对值换算为度数；与 `maxAngleErrorDeg` 比较。
- **重复性：** 同组内位置测量 `sqrt(sum((Xi-Xmean)^2+(Yi-Ymean)^2)/(n-1))`，即径向样本标准差，单位 mm。它不是最大误差或 3σ；需要至少两次有效检测。
- 毫米精度门禁要求真实标定和全部正样本的独立世界坐标标注；漏检或缺失标注不会自动视为合格。**±0.1 mm 不能仅由图像匹配成功或单位转换推导出来。**

若镜头畸变、透视关系、相机移动或 Eye-in-Hand 变化不可忽略，单一固定平面仿射可能无效，需另外标定、纠畸和转换；本工具没有实现通用机器人手眼坐标链。图像点定位精度 ≠ 机器人最终抓取精度。

## 2.1 独立标定验证点与证据

启用 `acceptance.maxPositionErrorMm` 或 `acceptance.maxRepeatabilityStdDevMm` 时，除了 `calibration` 仿射系数，还**必须提供** `calibrationEvidence` 和下列三个显式门限。否则程序直接拒绝此次验收，不会将未验证的矩阵当作合格标定：

- `maxCalibrationFitErrorMm`：参与拟合的标定点最大径向残差上限；
- `maxCalibrationHoldoutErrorMm`：**未参与拟合**的独立验证点最大径向残差上限；
- `maxCalibrationHoldoutRmsMm`：独立验证点径向均方根误差上限。

`calibrationEvidence.calibrationId` 必须对应现场保存的标定版本，`measurementMethod` 应记录独立测量装置及数据来源；这些字符串由使用者填写，工具只能检查非空，**无法证明真实来源**。各点包含 `row`、`column`、`xmm`、`ymm`，至少 4 个非共线拟合点和 3 个独立验证点，且不能复用相同图像坐标。使用完整视野的不同位置作为验证点，不能只验证中心附近。

评测将对已提供的仿射系数分别计算训练点与验证点的每点 `dx/dy`、径向距离、最大值及 RMS，并在 JSON 中输出 `calibrationAudit`。任一残差超限即不通过；这一步**验证已给出的标定系数，不会重新拟合、优化或偷偷覆盖它们**。

简单例子：某模型在已标注工件上误差为 0.05 mm，但独立标定验证点最大残差为 0.14 mm，则即使工件检测指标看似合格，整体验收仍应失败。标定残差通过并不证明重复性、机器人 TCP、镜头畸变或最终抓取精度通过；那些需要其他独立证据。

本仓库 `example.dataset.json` 中拟合/验证点和世界坐标全部是**人为合成的格式示例**，不构成设备实际校准记录，也不能用于签发 ±0.1 mm 认证报告。

标定证据也可以**独立验证**，不需要工业相机、原生图像库或 Windows。只需 .NET 8 SDK 和带 `calibration` / `calibrationEvidence` / `acceptance` 的 JSON 清单：

```powershell
dotnet run --project tools/JLVision.CalibrationAudit/JLVision.CalibrationAudit.csproj -- --manifest "C:\\vision-dataset\\dataset.json" --output artifacts/calibration-audit.json
```

工具输出各训练点、独立验证点的残差、最大偏差、RMS 和通过/失败状态；验证不通过返回非零退出码。它**不会重算拟合矩阵**，只审核现场传入的矩阵是否与记录的独立测量点一致。GitHub Actions 的示例验证使用合成数据，只证明审计代码可用。



## 2.2 标定点空间覆盖、超限点与热力图

配置 `calibrationEvidence.imageRegion` 为真实评测区域（`minRow/maxRow/minColumn/maxColumn`）。**它不是从标定点的最小外接矩形自动推断的**，否则未验证的视野边缘会被排除。必须保证所有标定点与独立验证点处于此区域内。

程序将图像区域划分为固定 **3×3 网格**，统计 `fit` 和 `holdout` 两类测量点在每格的样本数和最大径向残差。可在 `acceptance` 中设置 `minFitCalibrationGridCells` 与 `minHoldoutCalibrationGridCells`（有效范围 1–9），覆盖率不足会使审计失败。指标表示**有测量样本的网格数**，不代表网格内所有像素都完成了精度验证。

同时标记超出 `maxCalibrationFitErrorMm` 或 `maxCalibrationHoldoutErrorMm` 的实际测量点，保留点序号、图像坐标、X/Y 偏差、径向误差及其限值。**不会自动剔除异常点、重拟合矩阵或美化均方根结果。**

`JLVision.CalibrationAudit` 可以输出 JSON 和 SVG：

```powershell
dotnet run --project tools/JLVision.CalibrationAudit/JLVision.CalibrationAudit.csproj -- --manifest "C:\\vision-dataset\\dataset.json" --output artifacts/calibration-audit.json --heatmap artifacts/calibration-residual.svg
```

SVG 为*采样点残差区域图*：只有包含独立验证点的网格才根据实测最大残差显示颜色；无验证点的区域明确显示灰色 **NO HOLDOUT**。空白区不做插值，不能因此宣称该区域误差为 0。圆点标记拟合/独立验证点，红色圈标记超限点。可直接用浏览器打开 SVG 并查看每个点的误差提示。

CI 自动生成 `jlv-calibration-audit-demo`（JSON 和 SVG），但这些是**纯合成的演示数据**，不是相机实际视野的误差热力图。

## 3. 质量指标与门禁

配置位于清单的 `acceptance`：

| 字段 | 指标 / 单位 |
|---|---|
| `minPositiveFrames`, `minNegativeFrames` | 测试集正负样本最小数量 |
| `maxMissRate` | `FN/(TP+FN)`，0–1 |
| `maxFalsePositiveRate` | `FP/(FP+TN)`，0–1 |
| `maxP95MatchMs` | 离线图像匹配 P95 耗时，毫秒 |
| `maxPositionErrorMm` | 所有检测成功的已标注正样本中最大平面距离误差 |
| `maxAngleErrorDeg` | 所有已标注正样本中最大方向误差 |
| `maxRepeatabilityStdDevMm` | 所有 `repeatGroup` 的最大径向样本标准差 |

程序会计算 TP/TN/FP/FN、Precision、Recall、P95、覆盖数以及每条质量门禁的 `passed/observed/maximum/unit`，某条必要指标缺少样本或标注时按未通过处理。整体不合格返回非零退出码，详细报告仍写出。

匹配算法训练一次后对整个数据集复用模型，耗时统计**不含文件读取、相机曝光、数据传输、机器人下发或机械动作**；因此 P95 小于 1 s 也不意味着整机周期 ≤1 s。

## 4. 使用方法

清单示例：`tools/JLVision.DatasetBenchmark/example.dataset.json`，其中标定系数、坐标和阈值均是**演示数字**，必须由实际数据替换。建议将标注清单和图像存放在仓库外：

```powershell
dotnet run --project tools/JLVision.DatasetBenchmark/JLVision.DatasetBenchmark.csproj --configuration Debug -- --manifest "C:\\vision-dataset\\dataset.json" --algorithm both --output artifacts/acceptance-real.json
```

合成数据的 CI 只验证基础检测/负样本和统计代码逻辑，**不启用虚假的毫米精度门禁**。这套清单格式是第一版，后续可拓展相机标定误差、角度对称性、图像严重度分层、批次划分与置信区间。

## 5. 合并与交付条件

1. Windows x64 CI、NuGet 独立消费端和统计单元测试全部通过；
2. 客户真实图像按正负/不同工况充分采样，完成标注复核；
3. 独立确认像素—毫米标定与机器人坐标链，完成外部测量的精度验证；
4. 按目标产线真实输入分辨率、光照变化、连续运行时长测量 P95/P99 与内存；
5. `maxPositionErrorMm=0.1` 仅在真正要求**欧氏径向误差 ≤0.1 mm**的场景设置，轴向 ±0.1 mm 验收应另行定义，不能混为一谈。

目前 Draft PR 不应以合成数据成绩作为工业现场可靠性/精度认证。
