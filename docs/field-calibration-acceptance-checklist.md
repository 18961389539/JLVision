# JLVision 现场标定验收证据清单（工程师填写）

> 本表是收集证据的模板，不构成合格证书。**合成示例、CI 成功和算法匹配成功不能代替现场 ±0.1 mm 验收。**

## 1. 现场身份与独立真值

- [ ] 设备/工位唯一编号：________
- [ ] 工件型号、批次与定位基准：________
- [ ] 相机、镜头、光源、曝光、焦距、工作距离、分辨率及软件/模型版本：________
- [ ] 标定版本 calibrationId 和时间（UTC）：________
- [ ] 独立测量工具、校准证书有效期、测量人员与数据来源：________
- [ ] 保存原始标定 fit 点及**未参与拟合**的 holdout 点坐标
- [ ] 保存 imageRegion 有效 ROI（row / column），检查空间覆盖范围
- [ ] 确认独立标注、标定源数据和算法输出之间没有循环引用

## 2. 质量验证（需要原始记录）

- [ ] 至少 4 个非共线拟合点、3 个独立验证点；配置不同区域的覆盖门槛
- [ ] 校验 fit 最大误差、holdout 最大误差及 holdout RMS 三个独立限制
- [ ] 带有足量正样本及缺件/误匹配负样本的完整数据集及标签
- [ ] 记录漏检率、误检率、角度误差和匹配耗时 P95
- [ ] 在实际世界坐标中测量每帧位置误差（mm），而不只用像素误差
- [ ] 固定位置多次独立采集，确认重复性；更换装夹的样本不能混在同组
- [ ] 按照最差光照、反光、遮挡、批次、边缘视野、旋转角度组合测试
- [ ] 保留时间戳和可比较的标定版本历史、参考点和残差变化证据
- [ ] 长时间稳定性及内存/并发压力测试有原始报告

## 3. 机器人整体精度（独立于图像定位）

- [ ] 明确 Eye-to-Hand / Eye-in-Hand 坐标链，记录机器人与工具姿态
- [ ] 独立验证 TCP 偏心、J4 旋转中心补偿和视觉到机器人坐标转换
- [ ] 以物理工装/计量设备验证实际拾取位置误差及成功率
- [ ] 记录真实节拍（包含拍照、计算、通信、机器人动作），与项目目标比较

## 4. 放行判据

- [ ] 使用 `JLVision.CalibrationAudit` 的完整 JSON/热力图；没有验证过的区域标为 NO HOLDOUT
- [ ] 使用 `JLVision.DatasetBenchmark` 产出真实数据集评估报告
- [ ] 保存历史 HTML 报告与其对应的输入 JSON、门限、运行命令、代码提交 SHA
- [ ] 无任一必须证据缺失、无未处理的质量回退或验收失败
- [ ] 由现场工程师及质量负责人独立签署结果与日期

**现场结论（只能在实际验证后填写）：** 未执行 / 不通过 / 有条件通过 / 通过

工程师：________  质量负责人：________  日期：________

## 示例命令

```powershell
dotnet run --project tools/JLVision.CalibrationAudit -- --manifest "C:\\vision-data\\current.json" --history "C:\\vision-data\\history.json" --max-peak-increase-mm 0.03 --max-rms-increase-mm 0.02 --output artifacts/audit.json

python tools/calibration-history-report.py --input artifacts/audit.json --output artifacts/history.html --max-consecutive-degrading-steps 2
```

注意：第二条命令的阈值是人为选择的**示例**，须依据工艺和计量能力审批；退出码 `2` 表示报告门禁要求人工复核，`0` 表示当前已提供的软件证据满足这些门禁，并不表示机器人系统整体验收通过。

## 5. 机器可读的证据包（离线审核）

在工位本地建立证据目录，复制 `docs/field-evidence-manifest.template.json` 并填写所有现场信息。使用以下五类附件：

- `calibrationAudit`：标定审计 JSON（含独立验证点、history 结论）
- `datasetBenchmark`：真实图像数据集算法验收 JSON
- `rawMetrology`：外部计量设备导出的原始测量记录
- `robotPickTrials`：机器人实际抓取试验记录
- `soakTestLog`：长时间运行及稳定性日志

每类附件提供**相对于清单目录的路径和 SHA256**。清单不得用指向父目录的路径或符号链接逃逸证据目录；修改任何附件后必须重新计算摘要。

```powershell
python tools/field-evidence-audit.py --manifest "C:\\field-evidence\\manifest.json" --output "C:\\field-evidence\\audit-result.json" --markdown "C:\\field-evidence\\review.md"
```

输出 `audit-result.json` 包含机器状态、错误原因、文件校验和及附件大小，`review.md` 列出待补证据、人工复核签署栏。

- 退出码 **0**：仅表示 `READY_FOR_HUMAN_REVIEW`（机器检验的证据完整，可交给质量人员人工审核），**不是设备合格证**。
- 退出码 **2**：`REVIEW_REQUIRED`（缺失证据、哈希不一致、标定回退、算法门禁不通过、非现场或合成数据等）。
- 退出码 **1**：清单 JSON 不合法、CLI 输入或文件系统错误。

程序无法验证文件记录是否来自真实设备、测量工具的校准证书是否有效、独立标注的真伪，以及机器人最终实际抓取精度。必须由独立审核人员结合原始记录、物理计量和系统级试验做最终放行。不得将客户原始图像或计量数据提交到公共 GitHub 仓库。
