#!/usr/bin/env python3
"""Offline HTML rendering for JLVision recognition benchmark JSON.
No network, external fonts, scripts, images, or access to original production images.
"""
import argparse
import html
import json
import math
from pathlib import Path


def esc(value):
    return html.escape(str(value), quote=True)


def count(record, key):
    value = record.get(key)
    if type(value) is not int or value < 0:
        raise ValueError("Missing or invalid count: " + key)
    return value


def finite(value, name):
    if type(value) not in (int, float) or not math.isfinite(value):
        raise ValueError("Missing or nonfinite metric: " + name)
    return float(value)


def interval(record, key):
    value = record.get("uncertainty", {}).get(key)
    if not isinstance(value, dict):
        raise ValueError("Missing 95% Wilson interval: " + key)
    events, trials = count(value, "events"), count(value, "trials")
    if events > trials:
        raise ValueError("Impossible confidence interval counts: " + key)
    if trials == 0:
        if any(value.get(f) is not None for f in ("estimate", "lower95", "upper95")):
            raise ValueError("Zero-trial intervals must be undefined")
        return None
    estimate, lower, upper = (finite(value.get(k), k) for k in
                              ("estimate", "lower95", "upper95"))
    if not (0 <= lower <= estimate <= upper <= 1):
        raise ValueError("Invalid Wilson interval bounds: " + key)
    if abs(estimate - events / trials) > 1e-8:
        raise ValueError("Wilson estimate does not match counts: " + key)
    return estimate, lower, upper


def checked_metrics(record):
    if not isinstance(record, dict):
        raise ValueError("Invalid recognition metrics object")
    t, p, n = (count(record, x) for x in ("total", "positive", "negative"))
    tp, fn, tn, fp, wrong, errors = (count(record, x) for x in (
        "truePositive", "falseNegative", "trueNegative", "falsePositive",
        "misread", "processingErrors"))
    if p + n != t or tp + fn != p or tn + fp != n or wrong > fn or errors > t:
        raise ValueError("Inconsistent confusion matrix")
    ms = finite(record.get("p95Ms"), "p95Ms")
    if ms < 0:
        raise ValueError("Negative processing time")
    for key, denominator, numerator in (
        ("missRate", p, fn), ("misreadRate", p, wrong),
        ("falsePositiveRate", n, fp)):
        val = finite(record.get(key), key)
        expected = numerator / denominator if denominator else 1
        if abs(val - expected) > 1e-8:
            raise ValueError("Reported rate does not agree with confusion counts: " + key)
    intervals = {key: interval(record, key)
                 for key in ("correctRead", "miss", "misread", "falsePositive")}
    for name, expected_events, expected_trials in (
        ("correctRead", tp, p), ("miss", fn, p),
        ("misread", wrong, p), ("falsePositive", fp, n)):
        entry = record["uncertainty"][name]
        if (entry["events"], entry["trials"]) != (expected_events, expected_trials):
            raise ValueError("Wilson counts do not match confusion matrix: " + name)
    return intervals


def percent(value):
    return f"{value * 100:.1f}%"


def metric_row(label, record):
    rates = checked_metrics(record)
    cells = []
    for key in ("miss", "misread", "falsePositive"):
        bounds = rates[key]
        if bounds is None:
            cells.append('<span class="muted">N/A (n=0)</span>')
        else:
            estimate, low, high = bounds
            cells.append(
                '<div class="measure">'
                f'<span>{percent(estimate)}</span>'
                f'<small>95% CI {percent(low)}–{percent(high)}</small>'
                f'<div class="track"><i style="width:{min(100, high * 100):.2f}%"></i></div>'
                '</div>')
    return (f'<tr><th scope="row">{esc(label)}</th>'
            f'<td>{record["positive"]} / {record["negative"]}</td>'
            f'<td>{record["truePositive"]} / {record["falseNegative"]} / {record["falsePositive"]}</td>'
            + "".join(f"<td>{cell}</td>" for cell in cells)
            + f'<td class="number">{finite(record["p95Ms"], "p95Ms"):.2f}</td></tr>')


def table_block(title, entries):
    if not isinstance(entries, dict):
        raise ValueError("Invalid metrics group: " + title)
    rows = "".join(metric_row(k, v) for k, v in sorted(entries.items()))
    return (
        f'<section><h2>{esc(title)}</h2><div class="scroller"><table>'
        '<thead><tr><th scope="col">Group</th><th scope="col">Positive / Negative</th>'
        '<th scope="col">TP / FN / FP</th>'
        '<th scope="col">Miss rate</th><th scope="col">Misread rate</th>'
        '<th scope="col">False-positive rate</th><th scope="col">P95 ms</th></tr></thead>'
        f'<tbody>{rows or "<tr><td colspan=7>No labeled groups</td></tr>"}</tbody>'
        '</table></div></section>')


def frame_diagnostics(rows):
    """Show first 100 failing/uncertain frame IDs without copying decoded text."""
    if rows is None:
        return '<section><h2>Frame diagnostics</h2><p>Frame details were not supplied.</p></section>'
    if not isinstance(rows, list):
        raise ValueError("Invalid frame observation collection")
    failing = []
    for record in rows:
        if not isinstance(record, dict) or not all(
                isinstance(record.get(key), str) for key in ("id", "kind", "condition")):
            raise ValueError("Invalid frame observation identity")
        if any(type(record.get(key)) is not bool for key in
               ("expectedPresent", "detected", "accepted", "misread")):
            raise ValueError("Invalid frame observation labels")
        elapsed = finite(record.get("elapsedMs"), "frame elapsedMs")
        if elapsed < 0:
            raise ValueError("Negative frame elapsed time")
        if record.get("errorType") is not None and not isinstance(record["errorType"], str):
            raise ValueError("Invalid frame error type")
        error = record.get("errorType")
        if error:
            status = "DECODER ERROR"
        elif record["expectedPresent"] and record["accepted"]:
            continue
        elif record["expectedPresent"] and record["misread"]:
            status = "WRONG CONTENT / EXTRA SYMBOL"
        elif record["expectedPresent"]:
            status = "NOT CORRECTLY READ"
        elif record["detected"]:
            status = "FALSE POSITIVE"
        else:
            continue
        failing.append(
            f'<tr><th scope="row">{esc(record["id"])}</th>'
            f'<td>{esc(record["kind"])}</td>'
            f'<td>{esc(record["condition"])}</td>'
            f'<td>{esc(status)}</td><td>{elapsed:.2f}</td>'
            f'<td>{esc(error) if error else "—"}</td></tr>')
    rendered = "".join(failing[:100])
    extra = len(failing) - 100
    message = ("No per-frame failures observed. Review sample coverage and "
               "uncertainty intervals before any release.") if not failing else (
               f"{len(failing)} failing frame(s) found; shown {min(len(failing), 100)}."
               + (f" {extra} further rows omitted." if extra > 0 else ""))
    return (f'<section><h2>Frame diagnostics</h2><p>{esc(message)}</p>'
            '<p class="note">Identifiers and error types only; no recognized label '
            'strings or customer image pixels are embedded.</p>'
            '<div class="scroller"><table><thead><tr><th>Frame ID</th><th>Algorithm</th>'
            '<th>Condition</th><th>Outcome</th><th>ms</th><th>Error type</th>'
            f'</tr></thead><tbody>{rendered}</tbody></table></div></section>')


def render(report):
    if not isinstance(report, dict) or type(report.get("schemaVersion")) is not int or report["schemaVersion"] != 1:
        raise ValueError("Unsupported recognition benchmark schema")
    if report.get("sourceType") not in ("field", "synthetic"):
        raise ValueError("Invalid or missing source provenance label")
    if report.get("realFieldAccuracyCertified") is not False:
        raise ValueError("Report must not assert automatic field certification")
    verdict = report.get("verdict")
    allowed = ("REVIEW_REQUIRED", "SYNTHETIC_SOFTWARE_GATES_PASSED",
               "SOFTWARE_GATES_PASSED_HUMAN_REVIEW_REQUIRED")
    if verdict not in allowed:
        raise ValueError("Unexpected benchmark verdict")
    if not isinstance(report.get("reasons"), list) or not all(
            isinstance(x, str) for x in report["reasons"]):
        raise ValueError("Invalid failure reasons")
    if report["reasons"] and verdict != "REVIEW_REQUIRED":
        raise ValueError("Passing verdict conflicts with quality gate failures")
    if not report["reasons"] and verdict == "REVIEW_REQUIRED":
        raise ValueError("Missing quality gate failure reasons")
    if report["sourceType"] == "synthetic" and verdict == "SOFTWARE_GATES_PASSED_HUMAN_REVIEW_REQUIRED":
        raise ValueError("Synthetic data cannot claim field review status")
    if report["sourceType"] == "field" and verdict == "SYNTHETIC_SOFTWARE_GATES_PASSED":
        raise ValueError("Field data cannot claim a synthetic verdict")
    if not isinstance(report.get("acceptance"), dict):
        raise ValueError("Acceptance limits missing from report")
    overall = report.get("overall")
    rates = checked_metrics(overall)
    for field in ("datasetId", "declaredSoftwareCommit", "manifestSha256"):
        if not isinstance(report.get(field), str) or not report[field]:
            raise ValueError("Missing traceability field: " + field)
    title = "REVIEW REQUIRED" if report["reasons"] else "SOFTWARE GATES PASSED"
    provenance = ("SYNTHETIC DEMONSTRATION — NOT FIELD EVIDENCE" if
                  report["sourceType"] == "synthetic" else
                  "DECLARED FIELD DATA — HUMAN PROVENANCE REVIEW REQUIRED")
    items = "".join(f'<li>{esc(item)}</li>' for item in report["reasons"])
    if not items:
        items = "<li>No configured software gate violations; human review still required.</li>"
    limits = report["acceptance"]
    strict = limits.get("requireWilsonUpper95")
    if type(strict) is not bool:
        raise ValueError("Missing Wilson gate policy")
    limits_display = [
        ("Max miss rate", "maxMissRate", True),
        ("Max misread rate", "maxMisreadRate", True),
        ("Max false-positive rate", "maxFalsePositiveRate", True),
        ("Max P95", "maxP95Ms", False),
        ("Min positive / kind", "minPositivePerKind", False),
        ("Min negative / kind", "minNegativePerKind", False),
        ("Min positive / condition", "minPositivePerCondition", False),
        ("Min negative / condition", "minNegativePerCondition", False),
    ]
    threshold_parts = []
    for label, key, is_rate in limits_display:
        val = finite(limits.get(key), key)
        if is_rate and not 0 <= val <= 1:
            raise ValueError("Threshold rate out of bounds: " + key)
        if val < 0:
            raise ValueError("Negative threshold: " + key)
        formatted = percent(val) if is_rate else f'{val:g}{" ms" if key == "maxP95Ms" else ""}'
        threshold_parts.append(f'<div><dt>{esc(label)}</dt><dd>{esc(formatted)}</dd></div>')
    summary = [
        ("Correct read", overall["truePositive"]),
        ("Misses", overall["falseNegative"]),
        ("Misreads", overall["misread"]),
        ("False positives", overall["falsePositive"]),
        ("P95 processing", f'{overall["p95Ms"]:.2f} ms'),
    ]
    cards = "".join(f'<div class="stat"><span>{esc(k)}</span><strong>{esc(v)}</strong></div>'
                    for k, v in summary)
    sections = "".join((
        table_block("By algorithm", report.get("byKind")),
        table_block("By condition", report.get("byCondition")),
        table_block("By algorithm × condition", report.get("byKindCondition"))))
    diagnostics = frame_diagnostics(report.get("frames"))
    summary_bound = rates["miss"]
    confidence_note = ("No positive samples to estimate miss rate" if summary_bound is None else
                       f"Observed miss rate {percent(summary_bound[0])}, Wilson 95% upper bound {percent(summary_bound[2])} "
                       f"(n={overall['positive']} independently labeled positive images).")
    return f"""<!doctype html>
<html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>JLVision Recognition Acceptance — {esc(report["datasetId"])}</title>
<style>
:root{{color-scheme:dark;font-family:Inter,Segoe UI,Arial,sans-serif;background:#0c1423;color:#e6edf7}}
*{{box-sizing:border-box}}body{{max-width:1250px;margin:0 auto;padding:40px 24px 80px}}
header{{border-bottom:1px solid #344055;padding-bottom:22px;margin-bottom:22px}}
.brand{{font-weight:750;letter-spacing:.16em;color:#80bfff;font-size:12px}}
h1{{font-size:clamp(29px,4.5vw,48px);letter-spacing:-.03em;margin:10px 0}}
h2{{font-size:19px;margin:0 0 14px}}p{{color:#b9c6d9;line-height:1.55}}
.pill{{display:inline-block;border:1px solid #42536c;padding:6px 11px;border-radius:30px;font-size:12px;color:#cad7ec}}
strong.verdict{{display:block;font-size:25px;margin:14px 0 4px}}
section{{margin-top:24px;background:#142136;border:1px solid #293a54;border-radius:16px;padding:24px}}
.stats{{display:grid;grid-template-columns:repeat(auto-fit,minmax(150px,1fr));gap:12px}}
.stat{{padding:18px;background:#152941;border:1px solid #2c4560;border-radius:13px}}
.stat span{{display:block;color:#aac1de;font-size:12px;margin-bottom:12px}}
.stat strong{{font-size:25px;font-variant-numeric:tabular-nums}}
.scroller{{overflow-x:auto}}table{{border-collapse:collapse;width:100%;min-width:980px;font-size:13px}}
th,td{{padding:12px 13px;text-align:left;border-bottom:1px solid #304056;vertical-align:top}}
th{{color:#c7d9f2;font-weight:650}}tbody tr:hover{{background:#21334c}}
.measure{{min-width:125px;font-variant-numeric:tabular-nums}}
.measure small{{display:block;color:#97aac4;margin:5px 0 8px;font-size:11px}}
.track{{height:5px;border-radius:8px;background:#354761;overflow:hidden}}
.track i{{height:100%;display:block;background:#78bcf6;border-radius:8px}}
.muted{{color:#9baac0}}.number{{font-variant-numeric:tabular-nums}}
dl{{display:grid;grid-template-columns:repeat(auto-fit,minmax(170px,1fr));gap:16px}}
dl div{{border-bottom:1px solid #314459;padding-bottom:9px}}
dt{{font-size:12px;color:#a1b8d5}}dd{{margin:4px 0 0;font-weight:650}}
ul{{line-height:1.7}}li{{overflow-wrap:anywhere}}
code{{background:#22334b;color:#e0eefe;padding:2px 5px;border-radius:4px;overflow-wrap:anywhere}}
.note{{font-size:13px;color:#aabed7}}footer{{font-size:12px;color:#9aacc7;margin-top:24px}}
</style></head><body>
<header><div class="brand">JLVISION / RECOGNITION VALIDATION</div>
<h1>Industrial Recognition Benchmark</h1>
<span class="pill">{esc(provenance)}</span>
<strong class="verdict">{esc(title)}</strong>
<p>{esc(confidence_note)}</p></header>
<div class="stats">{cards}</div>
<section><h2>Acceptance decision</h2><p><b>Verdict:</b> {esc(verdict)}</p>
<ul>{items}</ul><p>Confidence intervals assume independent Bernoulli trials; repeated frames,
correlated failures and annotation bias invalidate that assumption. CI bounds describe statistical
uncertainty, not guaranteed field error rates.</p></section>
{sections}
{diagnostics}
<section><h2>Configured acceptance thresholds</h2>
<p>Wilson upper 95% bounds enforced: <b>{"YES" if strict else "NO (point-estimate gates only)"}</b></p>
<dl>{"".join(threshold_parts)}</dl>
<p class="note">P95 is observed nearest-rank processing time, not an upper confidence limit or whole robot cycle time.</p></section>
<section><h2>Evidence provenance</h2>
<p>Dataset: <code>{esc(report["datasetId"])}</code></p>
<p>Declared software commit: <code>{esc(report["declaredSoftwareCommit"])}</code></p>
<p>Manifest SHA256: <code>{esc(report["manifestSha256"])}</code></p>
<p>Source type: <code>{esc(report["sourceType"])}</code> (self-declared, not independently verified)</p>
<p><b>Real field accuracy certified:</b> NO. Raw image labels and external metrology require human verification.</p></section>
<footer>JLVision Offline Recognition Report · Static HTML, no remote resources or embedded customer images</footer>
</body></html>"""


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--input", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    payload = json.loads(args.input.read_text(encoding="utf-8"),
                         parse_constant=lambda x: (_ for _ in ()).throw(ValueError("Nonfinite JSON constant: " + x)))
    result = render(payload)
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(result, encoding="utf-8")
    print("HTML recognition report:", args.output)


if __name__ == "__main__":
    main()
