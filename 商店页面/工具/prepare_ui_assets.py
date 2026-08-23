from __future__ import annotations

import csv
from collections import deque
from dataclasses import dataclass
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageFont


ROOT = Path(__file__).resolve().parents[1]
OUTPUT_ROOT = ROOT / "可替换PNG"
AI_RETOUCH_ROOT = ROOT / "AI精修源"


@dataclass(frozen=True)
class AssetSpec:
    group: str
    filename: str
    target_size: tuple[int, int]


ASSETS = [
    AssetSpec("商店页面元素", "顶部金币.jpg", (48, 48)),
    AssetSpec("商店页面元素", "关闭按钮.png", (128, 128)),
    AssetSpec("商店页面元素", "横向木梁.jpg", (900, 80)),
    AssetSpec("商店页面元素", "滑动条.jpg", (34, 180)),
    AssetSpec("商店页面元素", "滑动条框.jpg", (36, 520)),
    AssetSpec("商店页面元素", "灰色按钮.jpg", (260, 90)),
    AssetSpec("商店页面元素", "基础卡牌.png", (300, 115)),
    AssetSpec("商店页面元素", "金币.jpg", (64, 64)),
    AssetSpec("商店页面元素", "金币袋图标.jpg", (160, 160)),
    AssetSpec("商店页面元素", "卷轴商品卡.jpg", (330, 500)),
    AssetSpec("商店页面元素", "卡牌底色.jpg", (300, 115)),
    AssetSpec("商店页面元素", "确认按钮.jpg", (260, 100)),
    AssetSpec("商店页面元素", "商店顶部木牌.jpg", (420, 140)),
    AssetSpec("商店页面元素", "商店主木质大面板.jpg", (1450, 780)),
    AssetSpec("商店页面元素", "石质角块.jpg", (130, 130)),
    AssetSpec("商店页面元素", "售卖木板.jpg", (260, 520)),
    AssetSpec("商店页面元素", "刷新图标.jpg", (64, 64)),
    AssetSpec("商店页面元素", "拖动至此处提示牌.jpg", (220, 70)),
    AssetSpec("商店页面元素", "指挥官头像.jpg", (96, 96)),
    AssetSpec("商店页面元素", "指挥官选择后的卡牌.png", (300, 115)),
    AssetSpec("商店页面元素", "指挥官选中.png", (300, 115)),
    AssetSpec("商店页面元素", "左侧卡牌列表背景栏.jpg", (390, 690)),
    AssetSpec("指挥官信息", "单个指挥官性格.jpg", (86, 36)),
    AssetSpec("指挥官信息", "队伍底板.jpg", (300, 92)),
    AssetSpec("指挥官信息", "队伍字样底板.jpg", (110, 58)),
    AssetSpec("指挥官信息", "性格按钮选中状态.jpg", (90, 40)),
    AssetSpec("指挥官信息", "指挥官头像.jpg", (300, 92)),
    AssetSpec("指挥官信息", "指挥官性格框.jpg", (180, 78)),
]

PRESERVE_NATIVE_ALPHA = {
    ("商店页面元素", "指挥官选中.png"),
}

AI_OVERRIDES = {
    ("商店页面元素", "灰色按钮.jpg"): AI_RETOUCH_ROOT / "灰色按钮.png",
    ("商店页面元素", "石质角块.jpg"): AI_RETOUCH_ROOT / "石质角块.png",
    ("商店页面元素", "指挥官头像.jpg"): AI_RETOUCH_ROOT / "指挥官头像.png",
}

KEEP_LARGEST_ONLY = {
    ("商店页面元素", "滑动条框.jpg"),
}

CENTRAL_VERTICAL_ONLY = {
    ("商店页面元素", "滑动条框.jpg"),
}


def kmeans(points: np.ndarray, cluster_count: int = 8) -> tuple[np.ndarray, np.ndarray]:
    if len(points) > 60000:
        step = max(1, len(points) // 60000)
        points = points[::step]
    values = points.astype(np.float32)
    luminance = values.mean(axis=1)
    order = np.argsort(luminance)
    positions = np.linspace(0, len(order) - 1, cluster_count, dtype=int)
    centers = values[order[positions]].copy()

    for _ in range(14):
        distances = ((values[:, None, :] - centers[None, :, :]) ** 2).sum(axis=2)
        labels = distances.argmin(axis=1)
        next_centers = centers.copy()
        for index in range(cluster_count):
            members = values[labels == index]
            if len(members):
                next_centers[index] = members.mean(axis=0)
        if np.max(np.abs(next_centers - centers)) < 0.25:
            centers = next_centers
            break
        centers = next_centers

    distances = ((values[:, None, :] - centers[None, :, :]) ** 2).sum(axis=2)
    labels = distances.argmin(axis=1)
    counts = np.bincount(labels, minlength=cluster_count)
    return centers, counts


def edge_samples(rgb: np.ndarray) -> np.ndarray:
    height, width = rgb.shape[:2]
    strip = max(8, int(min(width, height) * 0.09))
    parts = [
        rgb[:strip, :, :].reshape(-1, 3),
        rgb[-strip:, :, :].reshape(-1, 3),
        rgb[:, :strip, :].reshape(-1, 3),
        rgb[:, -strip:, :].reshape(-1, 3),
    ]
    return np.concatenate(parts, axis=0)


def infer_checker_palette(rgb: np.ndarray) -> tuple[np.ndarray, float]:
    samples = edge_samples(rgb)
    centers, counts = kmeans(samples)
    normalized = centers / 255.0
    saturation = (normalized.max(axis=1) - normalized.min(axis=1)) / np.maximum(
        normalized.max(axis=1), 0.05
    )
    fractions = counts / max(1, counts.sum())
    candidates = (saturation < 0.16) & (fractions > 0.012)
    palette = centers[candidates]

    if len(palette) < 2:
        ranked = np.argsort(counts)[::-1]
        palette = centers[ranked[: min(3, len(centers))]]

    confidence = float(fractions[candidates].sum()) if candidates.any() else 0.0
    return palette.astype(np.float32), confidence


def silhouette_from_chroma(rgb: np.ndarray) -> np.ndarray:
    values = rgb.astype(np.int16)
    chroma = values.max(axis=2) - values.min(axis=2)
    seed = np.where(chroma >= 18, 255, 0).astype(np.uint8)
    seed_image = Image.fromarray(seed, mode="L")

    # Remove isolated JPEG color noise, then close small gaps in the object outline.
    minimum_edge = min(rgb.shape[0], rgb.shape[1])
    open_size = 5
    close_size = min(15, max(7, int(minimum_edge / 64) | 1))
    seed_image = seed_image.filter(ImageFilter.MinFilter(open_size))
    seed_image = seed_image.filter(ImageFilter.MaxFilter(open_size))
    seed_image = seed_image.filter(ImageFilter.MaxFilter(close_size))
    seed_image = seed_image.filter(ImageFilter.MinFilter(close_size))

    # Flood the exterior. Any enclosed low-chroma area belongs to the UI object.
    exterior = Image.new("L", (seed_image.width + 4, seed_image.height + 4), 0)
    exterior.paste(seed_image, (2, 2))
    ImageDraw.floodfill(exterior, (0, 0), 128, thresh=1)
    exterior_array = np.asarray(exterior)[2:-2, 2:-2]
    silhouette = np.where(exterior_array == 128, 0, 255).astype(np.uint8)
    feathered = Image.fromarray(silhouette, mode="L").filter(
        ImageFilter.GaussianBlur(radius=max(0.8, minimum_edge / 1400.0))
    )
    return np.asarray(feathered)


def silhouette_from_light_checker(rgb: np.ndarray) -> np.ndarray:
    values = rgb.astype(np.int16)
    luminance = values.mean(axis=2)
    chroma = values.max(axis=2) - values.min(axis=2)
    seed = np.where((luminance < 238) | (chroma >= 14), 255, 0).astype(np.uint8)
    seed_image = Image.fromarray(seed, mode="L")
    seed_image = seed_image.filter(ImageFilter.MinFilter(5))
    seed_image = seed_image.filter(ImageFilter.MaxFilter(5))
    seed_image = seed_image.filter(ImageFilter.MaxFilter(11))
    seed_image = seed_image.filter(ImageFilter.MinFilter(11))
    exterior = Image.new("L", (seed_image.width + 4, seed_image.height + 4), 0)
    exterior.paste(seed_image, (2, 2))
    ImageDraw.floodfill(exterior, (0, 0), 128, thresh=1)
    exterior_array = np.asarray(exterior)[2:-2, 2:-2]
    silhouette = np.where(exterior_array == 128, 0, 255).astype(np.uint8)
    return np.asarray(
        Image.fromarray(silhouette, mode="L").filter(ImageFilter.GaussianBlur(1.0))
    )


def keep_significant_components(alpha: np.ndarray, largest_only: bool = False) -> np.ndarray:
    height, width = alpha.shape
    scale = min(1.0, 640.0 / max(width, height))
    sample_size = (max(1, int(width * scale)), max(1, int(height * scale)))
    sample = Image.fromarray(alpha, mode="L").resize(sample_size, Image.Resampling.NEAREST)
    foreground = np.asarray(sample) > 48
    visited = np.zeros_like(foreground, dtype=bool)
    components: list[list[tuple[int, int]]] = []
    sample_height, sample_width = foreground.shape

    for y in range(sample_height):
        for x in range(sample_width):
            if not foreground[y, x] or visited[y, x]:
                continue
            queue = deque([(y, x)])
            visited[y, x] = True
            component: list[tuple[int, int]] = []
            while queue:
                cy, cx = queue.popleft()
                component.append((cy, cx))
                for ny, nx in ((cy - 1, cx), (cy + 1, cx), (cy, cx - 1), (cy, cx + 1)):
                    if (
                        0 <= ny < sample_height
                        and 0 <= nx < sample_width
                        and foreground[ny, nx]
                        and not visited[ny, nx]
                    ):
                        visited[ny, nx] = True
                        queue.append((ny, nx))
            components.append(component)

    keep = np.zeros_like(foreground, dtype=np.uint8)
    largest_area = max((len(component) for component in components), default=0)
    minimum_area = max(10, int(largest_area * 0.0025))
    for component in components:
        if (largest_only and len(component) != largest_area) or len(component) < minimum_area:
            continue
        for y, x in component:
            keep[y, x] = 255
    keep_image = Image.fromarray(keep, mode="L").filter(ImageFilter.MaxFilter(3))
    keep_full = keep_image.resize((width, height), Image.Resampling.NEAREST)
    keep_array = np.asarray(keep_full).astype(np.float32) / 255.0
    return np.rint(alpha.astype(np.float32) * keep_array).astype(np.uint8)


def extract_alpha(
    image: Image.Image,
    preserve_native_alpha: bool = False,
    light_checker: bool = False,
    largest_only: bool = False,
    central_vertical_only: bool = False,
) -> tuple[Image.Image, str, float]:
    rgba = image.convert("RGBA")
    array = np.asarray(rgba).copy()
    existing_alpha = array[:, :, 3].copy()
    if preserve_native_alpha and existing_alpha.min() < 250:
        return rgba, "保留经检查的原生透明通道", float(np.mean(existing_alpha < 250))
    rgb = array[:, :, :3]
    palette, confidence = infer_checker_palette(rgb)
    silhouette_alpha = (
        silhouette_from_light_checker(rgb)
        if light_checker
        else silhouette_from_chroma(rgb)
    )
    if existing_alpha.min() < 250:
        silhouette_alpha = np.minimum(silhouette_alpha, existing_alpha)
        method = "清除PNG内烘焙棋盘格并保留原透明通道"
    else:
        method = (
            "亮度轮廓法处理AI精修源"
            if light_checker
            else "彩度轮廓法移除烘焙棋盘格"
        )
    array[:, :, 3] = keep_significant_components(
        silhouette_alpha, largest_only=largest_only
    )
    if central_vertical_only:
        center_x = array.shape[1] // 2
        half_width = max(12, int(min(array.shape[0] * 0.14, array.shape[1] * 0.09)))
        array[:, : max(0, center_x - half_width), 3] = 0
        array[:, min(array.shape[1], center_x + half_width) :, 3] = 0
    result = Image.fromarray(array, mode="RGBA")
    return result, method, confidence


def trim_transparent(image: Image.Image) -> Image.Image:
    alpha = image.getchannel("A")
    bbox = alpha.point(lambda value: 255 if value > 18 else 0).getbbox()
    if not bbox:
        return image
    left, top, right, bottom = bbox
    padding = max(4, int(max(right - left, bottom - top) * 0.012))
    left = max(0, left - padding)
    top = max(0, top - padding)
    right = min(image.width, right + padding)
    bottom = min(image.height, bottom + padding)
    return image.crop((left, top, right, bottom))


def fit_to_canvas(image: Image.Image, size: tuple[int, int]) -> Image.Image:
    target_width, target_height = size
    safe_width = max(1, int(target_width * 0.94))
    safe_height = max(1, int(target_height * 0.94))
    scale = min(safe_width / image.width, safe_height / image.height)
    resized_size = (
        max(1, int(round(image.width * scale))),
        max(1, int(round(image.height * scale))),
    )
    resized = image.resize(resized_size, Image.Resampling.LANCZOS)
    canvas = Image.new("RGBA", size, (0, 0, 0, 0))
    position = (
        (target_width - resized.width) // 2,
        (target_height - resized.height) // 2,
    )
    canvas.alpha_composite(resized, position)
    return canvas


def checker_background(size: tuple[int, int], tile: int = 18) -> Image.Image:
    width, height = size
    yy, xx = np.indices((height, width))
    light = np.array([224, 227, 230], dtype=np.uint8)
    dark = np.array([184, 190, 196], dtype=np.uint8)
    cells = ((xx // tile) + (yy // tile)) % 2
    rgb = np.where(cells[:, :, None] == 0, light, dark)
    return Image.fromarray(rgb.astype(np.uint8), mode="RGB").convert("RGBA")


def render_contact_sheet(
    records: list[dict[str, object]], background_mode: str, output_name: str
) -> None:
    cell_width, cell_height = 360, 250
    columns = 4
    rows = (len(records) + columns - 1) // columns
    sheet = Image.new("RGB", (columns * cell_width, rows * cell_height), "#20252b")
    draw = ImageDraw.Draw(sheet)
    font = ImageFont.load_default()

    for index, record in enumerate(records):
        x = (index % columns) * cell_width
        y = (index // columns) * cell_height
        preview_path = Path(str(record["natural_path"]))
        image = Image.open(preview_path).convert("RGBA")
        preview_area = (cell_width - 24, cell_height - 58)
        scale = min(preview_area[0] / image.width, preview_area[1] / image.height)
        preview = image.resize(
            (max(1, int(image.width * scale)), max(1, int(image.height * scale))),
            Image.Resampling.LANCZOS,
        )
        if background_mode == "checker":
            background = checker_background(preview.size, tile=12)
        else:
            background = Image.new("RGBA", preview.size, "#b21683")
        background.alpha_composite(preview)
        px = x + (cell_width - preview.width) // 2
        py = y + 8 + (preview_area[1] - preview.height) // 2
        sheet.paste(background.convert("RGB"), (px, py))
        draw.text((x + 12, y + cell_height - 42), str(record["name"]), fill="#f2ead8", font=font)
        draw.text(
            (x + 12, y + cell_height - 24),
            f'{record["natural_size"]} -> {record["target_size"]}',
            fill="#aab4bf",
            font=font,
        )

    sheet.save(OUTPUT_ROOT / output_name, quality=94, subsampling=0)


def make_contact_sheets(records: list[dict[str, object]]) -> None:
    render_contact_sheet(records, "checker", "透明裁切预览总表.jpg")
    render_contact_sheet(records, "solid", "透明裁切预览_高对比底.jpg")


def main() -> None:
    natural_root = OUTPUT_ROOT / "高分辨率透明裁切"
    unity_root = OUTPUT_ROOT / "Unity推荐尺寸"
    natural_root.mkdir(parents=True, exist_ok=True)
    unity_root.mkdir(parents=True, exist_ok=True)
    records: list[dict[str, object]] = []

    for spec in ASSETS:
        source = ROOT / spec.group / spec.filename
        if not source.exists():
            print(f"跳过缺失文件: {source}")
            continue

        processing_source = AI_OVERRIDES.get((spec.group, spec.filename), source)
        source_image = Image.open(processing_source)
        extracted, method, confidence = extract_alpha(
            source_image,
            preserve_native_alpha=(spec.group, spec.filename) in PRESERVE_NATIVE_ALPHA,
            light_checker=processing_source != source,
            largest_only=(spec.group, spec.filename) in KEEP_LARGEST_ONLY,
            central_vertical_only=(spec.group, spec.filename) in CENTRAL_VERTICAL_ONLY,
        )
        trimmed = trim_transparent(extracted)
        target = fit_to_canvas(trimmed, spec.target_size)

        natural_dir = natural_root / spec.group
        unity_dir = unity_root / spec.group
        natural_dir.mkdir(parents=True, exist_ok=True)
        unity_dir.mkdir(parents=True, exist_ok=True)
        output_name = source.stem + ".png"
        natural_path = natural_dir / output_name
        unity_path = unity_dir / output_name
        trimmed.save(natural_path, optimize=True)
        target.save(unity_path, optimize=True)

        records.append(
            {
                "group": spec.group,
                "name": output_name,
                "source_size": f"{source_image.width}x{source_image.height}",
                "natural_size": f"{trimmed.width}x{trimmed.height}",
                "target_size": f"{spec.target_size[0]}x{spec.target_size[1]}",
                "method": method,
                "processing_source": str(processing_source),
                "background_confidence": f"{confidence:.3f}",
                "natural_path": str(natural_path),
                "unity_path": str(unity_path),
            }
        )
        print(f"完成: {spec.group}/{spec.filename} -> {output_name}")

    report_path = OUTPUT_ROOT / "处理数据.csv"
    with report_path.open("w", encoding="utf-8-sig", newline="") as file:
        fieldnames = [
            "group",
            "name",
            "source_size",
            "natural_size",
            "target_size",
            "method",
            "processing_source",
            "background_confidence",
            "natural_path",
            "unity_path",
        ]
        writer = csv.DictWriter(file, fieldnames=fieldnames)
        writer.writeheader()
        writer.writerows(records)

    make_contact_sheets(records)
    print(f"\n已处理 {len(records)} 个素材，输出目录: {OUTPUT_ROOT}")


if __name__ == "__main__":
    main()
