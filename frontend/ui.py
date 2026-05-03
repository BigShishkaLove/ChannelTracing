import json
import os
import subprocess
import tkinter as tk
from collections import Counter
from tkinter import filedialog, ttk, messagebox

BACKEND_PROJECT = os.path.join("src", "src.csproj")


class App(tk.Tk):
    def __init__(self):
        super().__init__()
        self.title("Channel Tracing Viewer")
        self.geometry("1180x760")

        self.input_file = tk.StringVar()
        self.output_dir = tk.StringVar(value="output")
        self.algorithm = tk.StringVar(value="both")

        root = ttk.Frame(self)
        root.pack(fill=tk.BOTH, expand=True, padx=8, pady=8)

        self._build_controls(root)
        self._build_views(root)

    def _build_controls(self, root: ttk.Frame):
        controls = ttk.LabelFrame(root, text="Backend Run")
        controls.pack(fill=tk.X)

        ttk.Label(controls, text="Input file:").grid(row=0, column=0, padx=4, pady=4, sticky="w")
        ttk.Entry(controls, textvariable=self.input_file, width=85).grid(row=0, column=1, padx=4, pady=4)
        ttk.Button(controls, text="Browse", command=self.pick_input).grid(row=0, column=2, padx=4, pady=4)

        ttk.Label(controls, text="Output dir:").grid(row=1, column=0, padx=4, pady=4, sticky="w")
        ttk.Entry(controls, textvariable=self.output_dir, width=85).grid(row=1, column=1, padx=4, pady=4)
        ttk.Button(controls, text="Browse", command=self.pick_output).grid(row=1, column=2, padx=4, pady=4)

        ttk.Label(controls, text="Algorithm:").grid(row=2, column=0, padx=4, pady=4, sticky="w")
        ttk.Combobox(
            controls,
            textvariable=self.algorithm,
            values=["left", "yoshimura", "both"],
            state="readonly",
            width=24,
        ).grid(row=2, column=1, padx=4, pady=4, sticky="w")

        ttk.Button(controls, text="Run backend + Open result", command=self.run_backend_and_open).grid(row=2, column=2, padx=4, pady=4)

    def _build_views(self, root: ttk.Frame):
        actions = ttk.Frame(root)
        actions.pack(fill=tk.X, pady=6)
        ttk.Button(actions, text="Open Result JSON", command=self.open_json).pack(side=tk.LEFT, padx=4)
        ttk.Button(actions, text="Clear", command=self.clear).pack(side=tk.LEFT, padx=4)

        self.notebook = ttk.Notebook(root)
        self.notebook.pack(fill=tk.BOTH, expand=True)

        summary_frame = ttk.Frame(self.notebook)
        chart_frame = ttk.Frame(self.notebook)
        map_frame = ttk.Frame(self.notebook)

        self.notebook.add(summary_frame, text="Summary")
        self.notebook.add(chart_frame, text="Charts")
        self.notebook.add(map_frame, text="Routing map")

        self.summary_text = tk.Text(summary_frame, wrap=tk.WORD, font=("Segoe UI", 11))
        self.summary_text.pack(fill=tk.BOTH, expand=True)

        self.chart_canvas = tk.Canvas(chart_frame, bg="white")
        self.chart_canvas.pack(fill=tk.BOTH, expand=True)

        self.map_canvas = tk.Canvas(map_frame, bg="white")
        self.map_canvas.pack(fill=tk.BOTH, expand=True)

    def pick_input(self):
        path = filedialog.askopenfilename(filetypes=[("Text", "*.txt"), ("All files", "*.*")])
        if path:
            self.input_file.set(path)

    def pick_output(self):
        path = filedialog.askdirectory()
        if path:
            self.output_dir.set(path)

    def open_json(self):
        path = filedialog.askopenfilename(filetypes=[("JSON", "*.json"), ("All files", "*.*")])
        if path:
            self.show_json(path)

    def show_json(self, path: str):
        with open(path, "r", encoding="utf-8-sig") as f:
            data = json.load(f)
        self.render_summary(data)
        self.render_charts(data)
        self.render_map(data)

    def run_backend_and_open(self):
        input_file = self.input_file.get().strip()
        output_dir = self.output_dir.get().strip() or "output"
        algo = self.algorithm.get().strip() or "both"

        if not input_file:
            messagebox.showerror("Input required", "Выберите входной файл канала.")
            return

        os.makedirs(output_dir, exist_ok=True)
        cmd = ["dotnet", "run", "--project", BACKEND_PROJECT, "--", input_file, output_dir, algo]

        try:
            proc = subprocess.run(cmd, capture_output=True, text=True, check=False)
        except FileNotFoundError:
            messagebox.showerror("dotnet missing", "dotnet SDK не найден в PATH.")
            return

        if proc.returncode != 0:
            messagebox.showerror("Backend error", f"Code: {proc.returncode}\n\nSTDERR:\n{proc.stderr}")
            return

        target = self.resolve_result_json(output_dir, algo)
        if target is None:
            messagebox.showwarning("No result", "JSON результата не найден.")
            return

        self.show_json(target)

    @staticmethod
    def resolve_result_json(output_dir: str, algo: str) -> str | None:
        candidates = {
            "left": "left_edge_algorithm_latest.json",
            "yoshimura": "yoshimura_algorithm_latest.json",
            "both": "yoshimura_algorithm_latest.json",
        }
        path = os.path.join(output_dir, candidates.get(algo, candidates["both"]))
        return path if os.path.exists(path) else None

    def render_summary(self, data: dict):
        ch = data.get("channel", {})
        rs = data.get("result", {})
        segs = rs.get("segments", [])
        horizontal = sum(1 for s in segs if s.get("type", 0) == 0)
        vertical = len(segs) - horizontal

        lines = [
            "Краткая сводка",
            "=" * 60,
            f"Алгоритм: {data.get('algorithmName', '')}",
            f"Сохранено: {data.get('savedAt', '')}",
            f"Ширина канала: {ch.get('width', 0)}",
            f"Число цепей: {ch.get('nets', 0)}",
            f"Использовано треков: {rs.get('tracksUsed', 0)}",
            f"Суммарная длина проводов: {rs.get('wireLength', 0):.0f}",
            f"Конфликтов: {rs.get('conflictCount', 0)}",
            f"Время выполнения: {rs.get('executionMs', 0):.3f} ms",
            f"Сегменты: всего {len(segs)}, горизонтальных {horizontal}, вертикальных {vertical}",
        ]
        self.summary_text.delete("1.0", tk.END)
        self.summary_text.insert(tk.END, "\n".join(lines))

    def render_charts(self, data: dict):
        rs = data.get("result", {})
        segs = rs.get("segments", [])
        tracks = rs.get("tracksUsed", 1) or 1

        self.chart_canvas.delete("all")
        w = max(self.chart_canvas.winfo_width(), 900)
        h = max(self.chart_canvas.winfo_height(), 500)
        self.chart_canvas.config(scrollregion=(0, 0, w, h))

        # Pie-like block (simple proportional bars)
        horizontal = sum(1 for s in segs if s.get("type", 0) == 0)
        vertical = len(segs) - horizontal
        total = max(1, len(segs))

        x0, y0 = 40, 40
        bw = 420
        self.chart_canvas.create_text(x0, y0 - 18, text="Состав сегментов", anchor="w", font=("Segoe UI", 12, "bold"))
        h_w = bw * horizontal / total
        self.chart_canvas.create_rectangle(x0, y0, x0 + h_w, y0 + 36, fill="#4e79a7", outline="")
        self.chart_canvas.create_rectangle(x0 + h_w, y0, x0 + bw, y0 + 36, fill="#f28e2b", outline="")
        self.chart_canvas.create_text(x0, y0 + 54, text=f"Horizontal: {horizontal}", anchor="w")
        self.chart_canvas.create_text(x0 + 180, y0 + 54, text=f"Vertical: {vertical}", anchor="w")

        # Track occupancy histogram (horizontal segments by track)
        self.chart_canvas.create_text(40, 130, text="Занятость треков (горизонтальные сегменты)", anchor="w", font=("Segoe UI", 12, "bold"))
        occ = Counter(s.get("track", 0) for s in segs if s.get("type", 0) == 0)
        if not occ:
            return
        max_count = max(occ.values())

        left, top = 40, 160
        chart_w, chart_h = min(w - 80, 1050), 280
        bar_w = max(2, chart_w / tracks)

        for t in range(tracks):
            cnt = occ.get(t, 0)
            bh = 0 if max_count == 0 else chart_h * cnt / max_count
            x1 = left + t * bar_w
            y1 = top + chart_h - bh
            self.chart_canvas.create_rectangle(x1, y1, x1 + bar_w - 1, top + chart_h, fill="#59a14f", outline="")

        self.chart_canvas.create_rectangle(left, top, left + chart_w, top + chart_h, outline="#555")
        self.chart_canvas.create_text(left, top + chart_h + 20, text="Track ID →", anchor="w")
        self.chart_canvas.create_text(left, top - 10, text=f"max segments on track: {max_count}", anchor="w")

    def render_map(self, data: dict):
        rs = data.get("result", {})
        ch = data.get("channel", {})
        segs = rs.get("segments", [])
        width = ch.get("width", 0) or 1
        tracks = max(1, rs.get("tracksUsed", 1))

        self.map_canvas.delete("all")
        cw = max(self.map_canvas.winfo_width(), 1000)
        chh = max(self.map_canvas.winfo_height(), 500)

        margin = 30
        drawable_w = cw - 2 * margin
        drawable_h = chh - 2 * margin

        # downsample columns to canvas pixels
        col_scale = max(1.0, width / max(1, drawable_w))
        row_h = max(2.0, drawable_h / tracks)

        for s in segs:
            if s.get("type", 0) != 0:
                continue
            track = s.get("track", 0)
            start = s.get("start", 0)
            end = s.get("end", 0)
            x1 = margin + start / col_scale
            x2 = margin + end / col_scale
            y = margin + (track + 0.5) * row_h
            color = f"#{(s.get('netId',1)*2654435761)&0xFFFFFF:06x}"
            self.map_canvas.create_line(x1, y, x2, y, fill=color, width=max(1, int(row_h * 0.65)))

        self.map_canvas.create_rectangle(margin, margin, margin + drawable_w, margin + drawable_h, outline="#444")
        self.map_canvas.create_text(margin, 12, text="Routing map (downsampled horizontal segments)", anchor="w")

    def clear(self):
        self.summary_text.delete("1.0", tk.END)
        self.chart_canvas.delete("all")
        self.map_canvas.delete("all")


if __name__ == "__main__":
    App().mainloop()
