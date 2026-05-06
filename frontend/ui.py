import json
import os
import subprocess
import tkinter as tk
from collections import Counter, defaultdict
from tkinter import filedialog, ttk, messagebox

BACKEND_PROJECT = os.path.join("src", "src.csproj")
PAGE_SEGMENTS = 250


class App(tk.Tk):
    def __init__(self):
        super().__init__()
        self.title("Channel Tracing Viewer")
        self.geometry("1280x820")
        self.minsize(1100, 700)

        self.input_file = tk.StringVar()
        self.output_dir = tk.StringVar(value="output")
        self.algorithm = tk.StringVar(value="left")

        self.current_data = None
        self.current_segments = []
        self.current_page = 0

        self._configure_style()

        root = ttk.Frame(self, padding=10)
        root.pack(fill=tk.BOTH, expand=True)

        self._build_controls(root)
        self._build_views(root)

    def _configure_style(self):
        style = ttk.Style(self)
        style.theme_use("clam")
        style.configure("TLabelframe", padding=8)
        style.configure("TNotebook", tabposition="n")
        style.configure("Header.TLabel", font=("Segoe UI", 11, "bold"))

    def _build_controls(self, root: ttk.Frame):
        controls = ttk.LabelFrame(root, text="Backend Run")
        controls.pack(fill=tk.X)
        controls.columnconfigure(1, weight=1)

        ttk.Label(controls, text="Input file:").grid(row=0, column=0, padx=6, pady=6, sticky="w")
        ttk.Entry(controls, textvariable=self.input_file).grid(row=0, column=1, padx=6, pady=6, sticky="ew")
        ttk.Button(controls, text="Browse", command=self.pick_input).grid(row=0, column=2, padx=6, pady=6)

        ttk.Label(controls, text="Output dir:").grid(row=1, column=0, padx=6, pady=6, sticky="w")
        ttk.Entry(controls, textvariable=self.output_dir).grid(row=1, column=1, padx=6, pady=6, sticky="ew")
        ttk.Button(controls, text="Browse", command=self.pick_output).grid(row=1, column=2, padx=6, pady=6)

        ttk.Label(controls, text="Algorithm:").grid(row=2, column=0, padx=6, pady=6, sticky="w")
        ttk.Combobox(
            controls,
            textvariable=self.algorithm,
            values=["left", "yoshimura"],
            state="readonly",
            width=24,
        ).grid(row=2, column=1, padx=6, pady=6, sticky="w")

        ttk.Button(controls, text="Run backend + Open result", command=self.run_backend_and_open).grid(row=2, column=2, padx=6, pady=6)

    def _build_views(self, root: ttk.Frame):
        actions = ttk.Frame(root)
        actions.pack(fill=tk.X, pady=8)
        ttk.Button(actions, text="Open Result JSON", command=self.open_json).pack(side=tk.LEFT, padx=4)
        ttk.Button(actions, text="Clear", command=self.clear).pack(side=tk.LEFT, padx=4)

        self.notebook = ttk.Notebook(root)
        self.notebook.pack(fill=tk.BOTH, expand=True)

        summary_frame = ttk.Frame(self.notebook, padding=8)
        chart_frame = ttk.Frame(self.notebook, padding=8)
        map_frame = ttk.Frame(self.notebook, padding=8)

        self.notebook.add(summary_frame, text="Summary")
        self.notebook.add(chart_frame, text="Charts")
        self.notebook.add(map_frame, text="Routing map")

        self.summary_text = tk.Text(summary_frame, wrap=tk.WORD, font=("Segoe UI", 11), relief=tk.FLAT)
        summary_scroll = ttk.Scrollbar(summary_frame, orient="vertical", command=self.summary_text.yview)
        self.summary_text.configure(yscrollcommand=summary_scroll.set)
        self.summary_text.pack(side=tk.LEFT, fill=tk.BOTH, expand=True)
        summary_scroll.pack(side=tk.RIGHT, fill=tk.Y)

        chart_container = ttk.Frame(chart_frame)
        chart_container.pack(fill=tk.BOTH, expand=True)
        self.chart_canvas = tk.Canvas(chart_container, bg="#f9fafc", highlightthickness=0)
        chart_scroll_y = ttk.Scrollbar(chart_container, orient="vertical", command=self.chart_canvas.yview)
        chart_scroll_x = ttk.Scrollbar(chart_container, orient="horizontal", command=self.chart_canvas.xview)
        self.chart_canvas.configure(xscrollcommand=chart_scroll_x.set, yscrollcommand=chart_scroll_y.set)
        self.chart_canvas.grid(row=0, column=0, sticky="nsew")
        chart_scroll_y.grid(row=0, column=1, sticky="ns")
        chart_scroll_x.grid(row=1, column=0, sticky="ew")
        chart_container.rowconfigure(0, weight=1)
        chart_container.columnconfigure(0, weight=1)

        map_toolbar = ttk.Frame(map_frame)
        map_toolbar.pack(fill=tk.X, pady=(0, 8))
        ttk.Label(map_toolbar, text="Segment page:", style="Header.TLabel").pack(side=tk.LEFT)
        self.prev_button = ttk.Button(map_toolbar, text="◀ Prev", command=self.prev_page)
        self.prev_button.pack(side=tk.LEFT, padx=(8, 4))
        self.next_button = ttk.Button(map_toolbar, text="Next ▶", command=self.next_page)
        self.next_button.pack(side=tk.LEFT, padx=4)
        self.page_label = ttk.Label(map_toolbar, text="No data", style="Header.TLabel")
        self.page_label.pack(side=tk.LEFT, padx=12)

        self.map_stats = ttk.Label(map_toolbar, text="", foreground="#445")
        self.map_stats.pack(side=tk.RIGHT)

        map_container = ttk.Frame(map_frame)
        map_container.pack(fill=tk.BOTH, expand=True)
        self.map_canvas = tk.Canvas(map_container, bg="white", highlightthickness=0)
        map_scroll_y = ttk.Scrollbar(map_container, orient="vertical", command=self.map_canvas.yview)
        map_scroll_x = ttk.Scrollbar(map_container, orient="horizontal", command=self.map_canvas.xview)
        self.map_canvas.configure(xscrollcommand=map_scroll_x.set, yscrollcommand=map_scroll_y.set)
        self.map_canvas.grid(row=0, column=0, sticky="nsew")
        map_scroll_y.grid(row=0, column=1, sticky="ns")
        map_scroll_x.grid(row=1, column=0, sticky="ew")
        map_container.rowconfigure(0, weight=1)
        map_container.columnconfigure(0, weight=1)

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
        self.current_data = data
        self.render_summary(data)
        self.render_charts(data)
        self.prepare_map(data)

    def run_backend_and_open(self):
        input_file = self.input_file.get().strip()
        output_dir = self.output_dir.get().strip() or "output"
        algo = self.algorithm.get().strip() or "left"

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
        }
        path = os.path.join(output_dir, candidates.get(algo, candidates["left"]))
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
        w = max(self.chart_canvas.winfo_width(), 960)
        h = max(self.chart_canvas.winfo_height(), 620)

        horizontal = sum(1 for s in segs if s.get("type", 0) == 0)
        vertical = len(segs) - horizontal
        total = max(1, len(segs))

        x0, y0 = 40, 40
        bw = 500
        self.chart_canvas.create_text(x0, y0 - 18, text="Состав сегментов", anchor="w", font=("Segoe UI", 13, "bold"))
        h_w = bw * horizontal / total
        self.chart_canvas.create_rectangle(x0, y0, x0 + h_w, y0 + 40, fill="#4e79a7", outline="")
        self.chart_canvas.create_rectangle(x0 + h_w, y0, x0 + bw, y0 + 40, fill="#f28e2b", outline="")
        self.chart_canvas.create_text(x0, y0 + 62, text=f"Horizontal: {horizontal}", anchor="w", font=("Segoe UI", 10))
        self.chart_canvas.create_text(x0 + 180, y0 + 62, text=f"Vertical: {vertical}", anchor="w", font=("Segoe UI", 10))

        self.chart_canvas.create_text(40, 150, text="Занятость треков", anchor="w", font=("Segoe UI", 13, "bold"))
        occ = Counter(s.get("track", 0) for s in segs if s.get("type", 0) == 0)
        if not occ:
            self.chart_canvas.config(scrollregion=(0, 0, w, h))
            return

        max_count = max(occ.values())
        left, top = 40, 180
        chart_w, chart_h = max(900, min(1400, tracks * 22)), 320
        bar_w = max(3, chart_w / max(1, tracks))

        for t in range(tracks):
            cnt = occ.get(t, 0)
            bh = 0 if max_count == 0 else chart_h * cnt / max_count
            x1 = left + t * bar_w
            y1 = top + chart_h - bh
            self.chart_canvas.create_rectangle(x1, y1, x1 + bar_w - 1, top + chart_h, fill="#59a14f", outline="")

        self.chart_canvas.create_rectangle(left, top, left + chart_w, top + chart_h, outline="#555")
        self.chart_canvas.create_text(left, top + chart_h + 24, text="Track ID →", anchor="w")
        self.chart_canvas.create_text(left, top - 14, text=f"max segments on track: {max_count}", anchor="w")
        self.chart_canvas.config(scrollregion=(0, 0, left + chart_w + 80, top + chart_h + 80))

    def prepare_map(self, data: dict):
        rs = data.get("result", {})
        self.current_segments = [s for s in rs.get("segments", []) if s.get("type", 0) == 0]
        self.current_page = 0
        self.render_current_page()

    def render_current_page(self):
        if not self.current_data:
            return

        rs = self.current_data.get("result", {})
        ch = self.current_data.get("channel", {})
        width = ch.get("width", 0) or 1
        tracks = max(1, rs.get("tracksUsed", 1))
        segs = self.current_segments
        page_count = max(1, (len(segs) + PAGE_SEGMENTS - 1) // PAGE_SEGMENTS)
        self.current_page = min(self.current_page, page_count - 1)

        start_idx = self.current_page * PAGE_SEGMENTS
        end_idx = min(len(segs), start_idx + PAGE_SEGMENTS)
        page = segs[start_idx:end_idx]

        self.prev_button.config(state=(tk.NORMAL if self.current_page > 0 else tk.DISABLED))
        self.next_button.config(state=(tk.NORMAL if self.current_page < page_count - 1 else tk.DISABLED))
        self.page_label.config(text=f"Page {self.current_page + 1}/{page_count} · segments {start_idx + 1}-{end_idx}")

        by_track = defaultdict(int)
        for s in page:
            by_track[s.get("track", 0)] += 1
        most_loaded = sorted(by_track.items(), key=lambda x: x[1], reverse=True)[:3]
        hint = ", ".join(f"t{t}:{cnt}" for t, cnt in most_loaded) if most_loaded else "no segments"
        self.map_stats.config(text=f"Top tracks on page: {hint}")

        self.map_canvas.delete("all")
        margin_x, margin_y = 70, 40
        track_h = 24
        scale = 10
        drawable_w = max(1000, width * scale)
        drawable_h = max(600, tracks * track_h)

        for t in range(tracks):
            y = margin_y + t * track_h
            if t % 2 == 0:
                self.map_canvas.create_rectangle(margin_x, y, margin_x + drawable_w, y + track_h, fill="#f7f9fc", outline="")
            self.map_canvas.create_text(margin_x - 8, y + track_h / 2, text=str(t), anchor="e", fill="#6b7280", font=("Segoe UI", 9))

        for s in page:
            track = s.get("track", 0)
            x1 = margin_x + s.get("start", 0) * scale
            x2 = margin_x + s.get("end", 0) * scale
            y = margin_y + track * track_h + track_h / 2
            color = f"#{(s.get('netId', 1) * 2654435761) & 0xFFFFFF:06x}"
            self.map_canvas.create_line(x1, y, x2, y, fill=color, width=max(2, int(track_h * 0.45)))

        self.map_canvas.create_rectangle(margin_x, margin_y, margin_x + drawable_w, margin_y + drawable_h, outline="#94a3b8")
        self.map_canvas.create_text(margin_x, 16, text="Routing map by segment pages (horizontal only)", anchor="w", font=("Segoe UI", 12, "bold"))
        self.map_canvas.config(scrollregion=(0, 0, margin_x + drawable_w + 40, margin_y + drawable_h + 40))

    def prev_page(self):
        if self.current_page > 0:
            self.current_page -= 1
            self.render_current_page()

    def next_page(self):
        page_count = max(1, (len(self.current_segments) + PAGE_SEGMENTS - 1) // PAGE_SEGMENTS)
        if self.current_page < page_count - 1:
            self.current_page += 1
            self.render_current_page()

    def clear(self):
        self.current_data = None
        self.current_segments = []
        self.current_page = 0
        self.summary_text.delete("1.0", tk.END)
        self.chart_canvas.delete("all")
        self.map_canvas.delete("all")
        self.page_label.config(text="No data")
        self.map_stats.config(text="")


if __name__ == "__main__":
    App().mainloop()
