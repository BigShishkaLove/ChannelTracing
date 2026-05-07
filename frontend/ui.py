import json
import os
import subprocess
import tkinter as tk
from collections import Counter, defaultdict
from tkinter import filedialog, ttk, messagebox

BACKEND_PROJECT = os.path.join("src", "src.csproj")
PAGE_SEGMENTS = 250

# ── Colour palette ────────────────────────────────────────────────────────────
BG_APP    = "#0f1117"
BG_PANEL  = "#161b27"
BG_CARD   = "#1e2535"
BORDER    = "#2d3748"
TEXT_PRI  = "#e2e8f0"
TEXT_MUT  = "#a0aec0"
TEXT_HINT = "#718096"
ACCENT    = "#818cf8"
ACCENT2   = "#f472b6"
COL_GREEN = "#34d399"
COL_RED   = "#f87171"
BAR_COLORS = [
    "#818cf8", "#34d399", "#f472b6", "#fb923c",
    "#38bdf8", "#a78bfa", "#4ade80", "#facc15",
]


class App(tk.Tk):
    def __init__(self):
        super().__init__()
        self.title("Channel Tracing Viewer")
        self.geometry("1280x820")
        self.minsize(1100, 700)
        self.configure(bg=BG_APP)

        self.input_file  = tk.StringVar()
        self.output_dir  = tk.StringVar(value="output")
        self.algorithm   = tk.StringVar(value="left")
        self.chart_mode  = "detail"

        self.current_data     = None
        self.current_segments = []
        self.current_page     = 0
        self.map_zoom         = 1.0

        self._apply_dark_theme()

        root = ttk.Frame(self, padding=10)
        root.pack(fill=tk.BOTH, expand=True)

        self._build_controls(root)
        self._build_views(root)

    # ── Theme ─────────────────────────────────────────────────────────────────

    def _apply_dark_theme(self):
        style = ttk.Style(self)
        style.theme_use("clam")

        style.configure(".",
            background=BG_APP, foreground=TEXT_PRI,
            fieldbackground=BG_CARD, bordercolor=BORDER,
            troughcolor=BG_PANEL, font=("Segoe UI", 10))

        style.configure("TFrame",   background=BG_APP)
        style.configure("TLabel",   background=BG_APP, foreground=TEXT_PRI)
        style.configure("Muted.TLabel",  background=BG_APP, foreground=TEXT_HINT)
        style.configure("Header.TLabel", background=BG_APP,
                        foreground=TEXT_PRI, font=("Segoe UI", 11, "bold"))

        style.configure("TButton",
            background=BG_CARD, foreground=TEXT_PRI,
            bordercolor=BORDER, focuscolor=ACCENT,
            relief="flat", padding=(10, 6))
        style.map("TButton",
            background=[("active", BORDER)],
            foreground=[("active", TEXT_PRI)])

        style.configure("Accent.TButton",
            background=ACCENT, foreground="#ffffff",
            bordercolor=ACCENT, relief="flat", padding=(10, 6))
        style.map("Accent.TButton",
            background=[("active", "#6366f1")])

        style.configure("TCombobox",
            fieldbackground=BG_CARD, background=BG_CARD,
            foreground=TEXT_PRI, arrowcolor=TEXT_MUT,
            bordercolor=BORDER, selectbackground=ACCENT)

        style.configure("TEntry",
            fieldbackground=BG_CARD, foreground=TEXT_PRI,
            insertcolor=TEXT_PRI, bordercolor=BORDER)

        style.configure("TNotebook",     background=BG_PANEL, bordercolor=BORDER)
        style.configure("TNotebook.Tab",
            background=BG_PANEL, foreground=TEXT_HINT,
            padding=(14, 7), bordercolor=BORDER)
        style.map("TNotebook.Tab",
            background=[("selected", BG_APP)],
            foreground=[("selected", ACCENT)])

        style.configure("TScrollbar",
            background=BG_PANEL, troughcolor=BG_APP,
            arrowcolor=TEXT_HINT, bordercolor=BORDER)
        style.map("TScrollbar", background=[("active", BORDER)])

        style.configure("TSeparator", background=BORDER)

    # ── Controls ──────────────────────────────────────────────────────────────

    def _build_controls(self, root: ttk.Frame):
        hdr = ttk.Frame(root)
        hdr.pack(fill=tk.X, pady=(0, 2))
        ttk.Label(hdr, text="Параметры запуска", style="Muted.TLabel").pack(side=tk.LEFT)
        ttk.Separator(hdr, orient="horizontal").pack(
            fill=tk.X, expand=True, padx=(8, 0), pady=6)

        controls = ttk.Frame(root)
        controls.pack(fill=tk.X, pady=(0, 6))
        controls.columnconfigure(1, weight=1)

        def lbl(text, row):
            ttk.Label(controls, text=text, style="Muted.TLabel").grid(
                row=row, column=0, padx=(0, 8), pady=4, sticky="w")

        lbl("Входной файл:", 0)
        ttk.Entry(controls, textvariable=self.input_file).grid(
            row=0, column=1, pady=4, sticky="ew")
        ttk.Button(controls, text="Обзор", command=self.pick_input).grid(
            row=0, column=2, padx=(8, 0), pady=4)

        lbl("Выходная папка:", 1)
        ttk.Entry(controls, textvariable=self.output_dir).grid(
            row=1, column=1, pady=4, sticky="ew")
        ttk.Button(controls, text="Обзор", command=self.pick_output).grid(
            row=1, column=2, padx=(8, 0), pady=4)

        lbl("Алгоритм:", 2)
        ttk.Combobox(controls, textvariable=self.algorithm,
                     values=["left", "yoshimura"],
                     state="readonly", width=20).grid(
            row=2, column=1, pady=4, sticky="w")
        ttk.Button(controls, text="▶  Запустить",
                   command=self.run_backend_and_open,
                   style="Accent.TButton").grid(
            row=2, column=2, padx=(8, 0), pady=4)

    # ── Views (tabs) ──────────────────────────────────────────────────────────

    def _build_views(self, root: ttk.Frame):
        actions = ttk.Frame(root)
        actions.pack(fill=tk.X, pady=(0, 6))
        ttk.Button(actions, text="Очистить", command=self.clear).pack(
            side=tk.LEFT, padx=4)

        self.notebook = ttk.Notebook(root)
        self.notebook.pack(fill=tk.BOTH, expand=True)

        summary_frame = ttk.Frame(self.notebook, padding=10)
        chart_frame   = ttk.Frame(self.notebook, padding=10)
        map_frame     = ttk.Frame(self.notebook, padding=10)

        self.notebook.add(summary_frame, text="  Summary  ")
        self.notebook.add(chart_frame,   text="  Charts  ")
        self.notebook.add(map_frame,     text="  Routing Map  ")

        self._build_summary_tab(summary_frame)
        self._build_charts_tab(chart_frame)
        self._build_map_tab(map_frame)

    # ── Summary tab ───────────────────────────────────────────────────────────

    def _build_summary_tab(self, parent: ttk.Frame):
        # Metric cards row
        card_row = tk.Frame(parent, bg=BG_APP)
        card_row.pack(fill=tk.X, pady=(0, 12))

        self.m_tracks = self._make_metric_card(card_row, "Треков",           "—")
        self.m_nets   = self._make_metric_card(card_row, "Цепей",            "—")
        self.m_wire   = self._make_metric_card(card_row, "Длина проводов",   "—")
        self.m_conf   = self._make_metric_card(card_row, "Конфликты",        "—")
        for w in (self.m_tracks, self.m_nets, self.m_wire, self.m_conf):
            w["frame"].pack(side=tk.LEFT, expand=True, fill=tk.X, padx=(0, 8))

        # Details text block
        text_wrap = tk.Frame(parent, bg=BG_CARD,
                             highlightthickness=1, highlightbackground=BORDER)
        text_wrap.pack(fill=tk.BOTH, expand=True)

        self.summary_text = tk.Text(
            text_wrap, wrap=tk.WORD,
            font=("Segoe UI", 11), relief=tk.FLAT,
            bg=BG_CARD, fg=TEXT_MUT,
            insertbackground=TEXT_PRI,
            bd=0, highlightthickness=0,
            state=tk.DISABLED, padx=14, pady=12)
        summary_scroll = ttk.Scrollbar(
            text_wrap, orient="vertical", command=self.summary_text.yview)
        self.summary_text.configure(yscrollcommand=summary_scroll.set)
        summary_scroll.pack(side=tk.RIGHT, fill=tk.Y)
        self.summary_text.pack(side=tk.LEFT, fill=tk.BOTH, expand=True)

    def _make_metric_card(self, parent: tk.Frame, label: str, value: str) -> dict:
        f = tk.Frame(parent, bg=BG_CARD,
                     highlightthickness=1, highlightbackground=BORDER)
        tk.Label(f, text=label, bg=BG_CARD, fg=TEXT_HINT,
                 font=("Segoe UI", 9)).pack(anchor="w", padx=14, pady=(12, 0))
        val_lbl = tk.Label(f, text=value, bg=BG_CARD, fg=ACCENT,
                           font=("Segoe UI", 22, "bold"))
        val_lbl.pack(anchor="w", padx=14, pady=(2, 12))
        return {"frame": f, "label": val_lbl}

    # ── Charts tab ────────────────────────────────────────────────────────────

    def _build_charts_tab(self, parent: ttk.Frame):
        # Mode toggle toolbar
        toolbar = tk.Frame(parent, bg=BG_APP)
        toolbar.pack(fill=tk.X, pady=(0, 8))

        tk.Label(toolbar, text="Режим:", bg=BG_APP,
                 fg=TEXT_HINT, font=("Segoe UI", 10)).pack(side=tk.LEFT)

        self.btn_detail = tk.Button(
            toolbar, text="Детальный",
            bg=ACCENT, fg="#ffffff", activebackground="#6366f1", activeforeground="#ffffff",
            relief="flat", bd=0, font=("Segoe UI", 10), padx=14, pady=5,
            cursor="hand2", command=lambda: self._set_chart_mode("detail"))
        self.btn_detail.pack(side=tk.LEFT, padx=(6, 0))

        self.btn_agg = tk.Button(
            toolbar, text="Агрегированный",
            bg=BG_CARD, fg=TEXT_MUT, activebackground=BORDER, activeforeground=TEXT_PRI,
            relief="flat", bd=0, font=("Segoe UI", 10), padx=14, pady=5,
            cursor="hand2", command=lambda: self._set_chart_mode("agg"))
        self.btn_agg.pack(side=tk.LEFT, padx=(4, 0))

        # Scrollable canvas
        container = tk.Frame(parent, bg=BG_APP)
        container.pack(fill=tk.BOTH, expand=True)
        container.rowconfigure(0, weight=1)
        container.columnconfigure(0, weight=1)

        self.chart_canvas = tk.Canvas(container, bg=BG_APP, highlightthickness=0)
        csy = ttk.Scrollbar(container, orient="vertical",
                            command=self.chart_canvas.yview)
        csx = ttk.Scrollbar(container, orient="horizontal",
                            command=self.chart_canvas.xview)
        self.chart_canvas.configure(
            xscrollcommand=csx.set, yscrollcommand=csy.set)
        self.chart_canvas.grid(row=0, column=0, sticky="nsew")
        csy.grid(row=0, column=1, sticky="ns")
        csx.grid(row=1, column=0, sticky="ew")

    def _set_chart_mode(self, mode: str):
        self.chart_mode = mode
        self.btn_detail.config(
            bg=ACCENT   if mode == "detail" else BG_CARD,
            fg="#ffffff" if mode == "detail" else TEXT_MUT)
        self.btn_agg.config(
            bg=ACCENT   if mode == "agg"    else BG_CARD,
            fg="#ffffff" if mode == "agg"   else TEXT_MUT)
        if self.current_data:
            self.render_charts(self.current_data)

    # ── Map tab ───────────────────────────────────────────────────────────────

    def _build_map_tab(self, parent: ttk.Frame):
        toolbar = tk.Frame(parent, bg=BG_APP)
        toolbar.pack(fill=tk.X, pady=(0, 8))

        self.prev_button = ttk.Button(toolbar, text="◀ Пред.", command=self.prev_page)
        self.prev_button.pack(side=tk.LEFT, padx=(0, 4))
        self.next_button = ttk.Button(toolbar, text="След. ▶", command=self.next_page)
        self.next_button.pack(side=tk.LEFT, padx=(0, 8))

        self.page_label = ttk.Label(toolbar, text="Нет данных", style="Muted.TLabel")
        self.page_label.pack(side=tk.LEFT, padx=(0, 12))

        ttk.Separator(toolbar, orient="vertical").pack(
            side=tk.LEFT, fill=tk.Y, padx=8, pady=3)

        ttk.Button(toolbar, text="−", width=3,
                   command=lambda: self.change_zoom(0.80)).pack(side=tk.LEFT, padx=(0, 2))
        ttk.Button(toolbar, text="+", width=3,
                   command=lambda: self.change_zoom(1.25)).pack(side=tk.LEFT, padx=(0, 6))
        self.zoom_label = ttk.Label(toolbar, text="100%", style="Muted.TLabel")
        self.zoom_label.pack(side=tk.LEFT)

        self.map_stats = ttk.Label(toolbar, text="", style="Muted.TLabel")
        self.map_stats.pack(side=tk.RIGHT)

        container = tk.Frame(parent, bg=BG_APP)
        container.pack(fill=tk.BOTH, expand=True)
        container.rowconfigure(0, weight=1)
        container.columnconfigure(0, weight=1)

        self.map_canvas = tk.Canvas(container, bg=BG_APP, highlightthickness=0)
        msy = ttk.Scrollbar(container, orient="vertical",
                            command=self.map_canvas.yview)
        msx = ttk.Scrollbar(container, orient="horizontal",
                            command=self.map_canvas.xview)
        self.map_canvas.configure(
            xscrollcommand=msx.set, yscrollcommand=msy.set)
        self.map_canvas.bind("<Control-MouseWheel>", self.on_zoom_mousewheel)
        self.map_canvas.grid(row=0, column=0, sticky="nsew")
        msy.grid(row=0, column=1, sticky="ns")
        msx.grid(row=1, column=0, sticky="ew")

    # ── File helpers ──────────────────────────────────────────────────────────

    def pick_input(self):
        path = filedialog.askopenfilename(
            filetypes=[("Text", "*.txt"), ("All files", "*.*")])
        if path:
            self.input_file.set(path)

    def pick_output(self):
        path = filedialog.askdirectory()
        if path:
            self.output_dir.set(path)

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
        algo       = self.algorithm.get().strip() or "left"

        if not input_file:
            messagebox.showerror("Input required", "Выберите входной файл канала.")
            return

        os.makedirs(output_dir, exist_ok=True)
        cmd = ["dotnet", "run", "--project", BACKEND_PROJECT,
               "--", input_file, output_dir, algo]

        try:
            proc = subprocess.run(cmd, capture_output=True, text=True, check=False)
        except FileNotFoundError:
            messagebox.showerror("dotnet missing", "dotnet SDK не найден в PATH.")
            return

        if proc.returncode != 0:
            messagebox.showerror(
                "Backend error",
                f"Code: {proc.returncode}\n\nSTDERR:\n{proc.stderr}")
            return

        target = self.resolve_result_json(output_dir, algo)
        if target is None:
            messagebox.showwarning("No result", "JSON результата не найден.")
            return

        self.show_json(target)

    @staticmethod
    def resolve_result_json(output_dir: str, algo: str) -> "str | None":
        candidates = {
            "left":      "left_edge_algorithm_latest.json",
            "yoshimura": "yoshimura_algorithm_latest.json",
        }
        path = os.path.join(output_dir, candidates.get(algo, candidates["left"]))
        return path if os.path.exists(path) else None

    # ── Render: Summary ───────────────────────────────────────────────────────

    def render_summary(self, data: dict):
        ch    = data.get("channel", {})
        rs    = data.get("result", {})
        segs  = rs.get("segments", [])
        h_cnt = sum(1 for s in segs if s.get("type", 0) == 0)
        v_cnt = len(segs) - h_cnt
        conf  = rs.get("conflictCount", 0)

        self.m_tracks["label"].config(
            text=str(rs.get("tracksUsed", "—")), fg=ACCENT)
        self.m_nets["label"].config(
            text=str(ch.get("nets", "—")), fg=TEXT_PRI)
        self.m_wire["label"].config(
            text=f'{rs.get("wireLength", 0):.0f}', fg=TEXT_PRI)
        self.m_conf["label"].config(
            text=str(conf), fg=COL_RED if conf > 0 else COL_GREEN)

        lines = "\n".join([
            f"Алгоритм:          {data.get('algorithmName', '')}",
            f"Сохранено:         {data.get('savedAt', '')}",
            f"Ширина канала:     {ch.get('width', 0)}",
            f"Время выполнения:  {rs.get('executionMs', 0):.3f} ms",
            f"Сегментов всего:   {len(segs)}"
            f"  (горизонт.: {h_cnt}, вертикал.: {v_cnt})",
        ])
        self.summary_text.config(state=tk.NORMAL)
        self.summary_text.delete("1.0", tk.END)
        self.summary_text.insert(tk.END, lines)
        self.summary_text.config(state=tk.DISABLED)

    # ── Render: Charts ────────────────────────────────────────────────────────

    def render_charts(self, data: dict):
        rs     = data.get("result", {})
        segs   = rs.get("segments", [])
        tracks = rs.get("tracksUsed", 1) or 1

        self.chart_canvas.delete("all")
        cw = max(self.chart_canvas.winfo_width(), 900)

        # ── 1. Segment composition bar ────────────────────────────────────────
        h_cnt  = sum(1 for s in segs if s.get("type", 0) == 0)
        v_cnt  = len(segs) - h_cnt
        total  = max(1, len(segs))
        x0, y0 = 50, 36
        bar_w  = min(560, cw - 120)

        self.chart_canvas.create_text(
            x0, y0 - 16, text="Состав сегментов",
            anchor="w", font=("Segoe UI", 12, "bold"), fill=TEXT_PRI)

        # background track
        self.chart_canvas.create_rectangle(
            x0, y0, x0 + bar_w, y0 + 26, fill=BG_CARD, outline=BORDER)
        # horizontal fill
        hw = max(0, int(bar_w * h_cnt / total))
        if hw > 0:
            self.chart_canvas.create_rectangle(
                x0, y0, x0 + hw, y0 + 26, fill=ACCENT, outline="")
        # vertical fill
        if hw < bar_w:
            self.chart_canvas.create_rectangle(
                x0 + hw, y0, x0 + bar_w, y0 + 26, fill=ACCENT2, outline="")

        # legend
        self.chart_canvas.create_text(
            x0, y0 + 40,
            text=f"● Горизонтальные: {h_cnt}  ({100 * h_cnt // total}%)",
            anchor="w", fill=ACCENT, font=("Segoe UI", 10))
        self.chart_canvas.create_text(
            x0 + 280, y0 + 40,
            text=f"● Вертикальные: {v_cnt}  ({100 * v_cnt // total}%)",
            anchor="w", fill=ACCENT2, font=("Segoe UI", 10))

        # ── 2. Track occupation histogram ──────────────────────────────────────
        hist_top  = y0 + 72
        hist_h    = 280
        hist_left = 58
        hist_bot  = hist_top + hist_h

        mode_lbl = " (агрегированный)" if self.chart_mode == "agg" else ""
        self.chart_canvas.create_text(
            x0, hist_top - 18,
            text=f"Занятость треков{mode_lbl}",
            anchor="w", font=("Segoe UI", 12, "bold"), fill=TEXT_PRI)

        occ = Counter(
            s.get("track", 0) for s in segs if s.get("type", 0) == 0)

        if self.chart_mode == "detail":
            bar_data   = [occ.get(t, 0) for t in range(tracks)]
            bar_labels = [str(t) for t in range(tracks)]
        else:
            bucket = max(1, tracks // 80)
            bar_data, bar_labels = [], []
            for i in range(0, tracks, bucket):
                end = min(tracks, i + bucket)
                bar_data.append(max(occ.get(t, 0) for t in range(i, end)))
                bar_labels.append(str(i))

        if not bar_data:
            self.chart_canvas.config(
                scrollregion=(0, 0, cw, hist_bot + 60))
            return

        max_val  = max(bar_data) or 1
        n_bars   = len(bar_data)
        chart_w  = max(cw - 120, n_bars * 8)
        bar_unit = chart_w / n_bars

        # Horizontal grid lines (5 levels: 0 % … 100 %)
        grid_levels = [
            (1.00, str(max_val)),
            (0.75, str(round(max_val * 0.75))),
            (0.50, str(round(max_val * 0.50))),
            (0.25, str(round(max_val * 0.25))),
            (0.00, "0"),
        ]
        for frac, label in grid_levels:
            gy = hist_bot - int(hist_h * frac)
            self.chart_canvas.create_line(
                hist_left, gy, hist_left + chart_w, gy,
                fill=BORDER, dash=(4, 4))
            self.chart_canvas.create_text(
                hist_left - 6, gy, text=label,
                anchor="e", fill=TEXT_HINT, font=("Segoe UI", 9))

        # Bars
        label_step = max(1, n_bars // 25)
        for i, val in enumerate(bar_data):
            bh = max(1, int(hist_h * val / max_val)) if val else 0
            x1 = hist_left + i * bar_unit
            x2 = x1 + max(1, bar_unit - 1)
            y1 = hist_bot - bh
            color = BAR_COLORS[i % len(BAR_COLORS)]
            if bh > 0:
                self.chart_canvas.create_rectangle(
                    x1, y1, x2, hist_bot, fill=color, outline="")
            # X-axis labels
            if i % label_step == 0:
                self.chart_canvas.create_text(
                    x1 + bar_unit / 2, hist_bot + 14,
                    text=bar_labels[i],
                    anchor="n", fill=TEXT_HINT, font=("Segoe UI", 8))

        # X-axis label title
        self.chart_canvas.create_text(
            hist_left + chart_w / 2, hist_bot + 34,
            text="Номер трека →",
            anchor="n", fill=TEXT_HINT, font=("Segoe UI", 9, "italic"))

        # Border
        self.chart_canvas.create_rectangle(
            hist_left, hist_top, hist_left + chart_w, hist_bot,
            outline=BORDER)

        self.chart_canvas.config(
            scrollregion=(0, 0, hist_left + chart_w + 60, hist_bot + 60))

    # ── Render: Map ───────────────────────────────────────────────────────────

    def prepare_map(self, data: dict):
        rs = data.get("result", {})
        self.current_segments = [
            s for s in rs.get("segments", []) if s.get("type", 0) == 0]
        self.current_page = 0
        self.render_current_page()

    def render_current_page(self):
        if not self.current_data:
            return

        rs     = self.current_data.get("result", {})
        ch     = self.current_data.get("channel", {})
        width  = ch.get("width", 0) or 1
        tracks = max(1, rs.get("tracksUsed", 1))
        segs   = self.current_segments

        page_count = max(1, (len(segs) + PAGE_SEGMENTS - 1) // PAGE_SEGMENTS)
        self.current_page = min(self.current_page, page_count - 1)
        start_idx = self.current_page * PAGE_SEGMENTS
        end_idx   = min(len(segs), start_idx + PAGE_SEGMENTS)
        page      = segs[start_idx:end_idx]

        self.prev_button.config(
            state=tk.NORMAL if self.current_page > 0 else tk.DISABLED)
        self.next_button.config(
            state=tk.NORMAL if self.current_page < page_count - 1 else tk.DISABLED)
        self.page_label.config(
            text=f"Стр. {self.current_page + 1}/{page_count}"
                 f"  ·  сег. {start_idx + 1}–{end_idx}")

        by_track = Counter(s.get("track", 0) for s in page)
        top3 = ", ".join(f"t{t}:{c}" for t, c in by_track.most_common(3))
        self.map_stats.config(text=f"Топ треки: {top3}" if top3 else "")

        self.map_canvas.delete("all")

        margin_x   = 64   # Y-axis label area
        margin_bot = 26   # X-axis label area
        margin_top = 8

        track_h = max(6, int(20 * self.map_zoom))
        scale   = max(0.05, 8.0 * self.map_zoom)

        drawable_w = max(600, int(width * scale))
        drawable_h = max(300, tracks * track_h)
        total_w    = margin_x + drawable_w
        total_h    = margin_top + drawable_h + margin_bot

        # ── Zebra track background ─────────────────────────────────────────────
        for t in range(tracks):
            y   = margin_top + t * track_h
            bg  = BG_PANEL if t % 2 == 0 else BG_APP
            self.map_canvas.create_rectangle(
                margin_x, y,
                margin_x + drawable_w, y + track_h,
                fill=bg, outline="")

        # ── Y-axis: track labels ───────────────────────────────────────────────
        tick_tracks = max(1, tracks // 40)
        for t in range(0, tracks, tick_tracks):
            y = margin_top + t * track_h + track_h // 2
            self.map_canvas.create_text(
                margin_x - 6, y, text=str(t),
                anchor="e", fill=TEXT_HINT, font=("Segoe UI", 9))

        # ── X-axis: column labels ──────────────────────────────────────────────
        tick_cols = max(1, width // 20)
        ax_y = margin_top + drawable_h

        self.map_canvas.create_line(
            margin_x, ax_y,
            margin_x + drawable_w, ax_y,
            fill=BORDER)

        for col in range(0, width + 1, tick_cols):
            x = margin_x + int(col * scale)
            self.map_canvas.create_line(x, ax_y, x, ax_y + 5, fill=BORDER)
            self.map_canvas.create_text(
                x, ax_y + 7, text=str(col),
                anchor="n", fill=TEXT_HINT, font=("Segoe UI", 8))

        # ── Segments (with viewport culling for large channels) ────────────────
        xl, xr  = self.map_canvas.xview()
        vis_l   = xl * (total_w + 20) - margin_x
        vis_r   = xr * (total_w + 20) - margin_x
        seg_h   = max(1, int(track_h * 0.40))

        for s in page:
            sc = s.get("start", 0) * scale
            ec = s.get("end",   0) * scale
            if ec < vis_l or sc > vis_r:
                continue
            if (ec - sc) < 0.5:
                continue
            t  = s.get("track", 0)
            x1 = margin_x + sc
            x2 = margin_x + ec
            y  = margin_top + t * track_h + track_h // 2
            color = f"#{(s.get('netId', 1) * 2654435761) & 0xFFFFFF:06x}"
            self.map_canvas.create_line(
                x1, y, x2, y, fill=color, width=seg_h)

        # ── Border + scrollregion ──────────────────────────────────────────────
        self.map_canvas.create_rectangle(
            margin_x, margin_top,
            margin_x + drawable_w, margin_top + drawable_h,
            outline=BORDER)

        self.map_canvas.config(
            scrollregion=(0, 0, total_w + 20, total_h + 10))
        self.zoom_label.config(text=f"{int(self.map_zoom * 100)}%")

    # ── Zoom ──────────────────────────────────────────────────────────────────

    def change_zoom(self, factor: float):
        # No upper limit; lower limit 1% so canvas doesn't vanish
        self.map_zoom = max(0.01, self.map_zoom * factor)
        self.render_current_page()

    def on_zoom_mousewheel(self, event):
        self.change_zoom(1.12 if event.delta > 0 else 0.88)

    # ── Page nav ──────────────────────────────────────────────────────────────

    def prev_page(self):
        if self.current_page > 0:
            self.current_page -= 1
            self.render_current_page()

    def next_page(self):
        page_count = max(
            1, (len(self.current_segments) + PAGE_SEGMENTS - 1) // PAGE_SEGMENTS)
        if self.current_page < page_count - 1:
            self.current_page += 1
            self.render_current_page()

    # ── Clear ─────────────────────────────────────────────────────────────────

    def clear(self):
        self.current_data     = None
        self.current_segments = []
        self.current_page     = 0
        self.map_zoom         = 1.0

        for card in (self.m_tracks, self.m_nets, self.m_wire, self.m_conf):
            card["label"].config(text="—", fg=ACCENT)

        self.summary_text.config(state=tk.NORMAL)
        self.summary_text.delete("1.0", tk.END)
        self.summary_text.config(state=tk.DISABLED)

        self.chart_canvas.delete("all")
        self.map_canvas.delete("all")
        self.page_label.config(text="Нет данных")
        self.map_stats.config(text="")
        self.zoom_label.config(text="100%")


if __name__ == "__main__":
    App().mainloop()