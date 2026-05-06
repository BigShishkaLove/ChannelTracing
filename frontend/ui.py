import json
import os
import subprocess
import tkinter as tk
from collections import Counter, defaultdict
from tkinter import filedialog, ttk, messagebox

BACKEND_PROJECT = os.path.join("src", "src.csproj")
PAGE_SEGMENTS = 250

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
BAR_COLORS = ["#818cf8","#34d399","#f472b6","#fb923c",
              "#38bdf8","#a78bfa","#4ade80","#facc15"]


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
        self.map_zoom = 1.0

        self._apply_dark_theme()

        root = ttk.Frame(self, padding=10, bg=BG_APP, style="TFrame")
        root.pack(fill=tk.BOTH, expand=True)

        self._build_controls(root)
        self._build_views(root)
    
    def _apply_dark_theme(self):
        self.configure(bg=BG_APP)
        style = ttk.Style(self)
        style.theme_use("clam")

        style.configure(".",
            background=BG_APP, foreground=TEXT_PRI,
            fieldbackground=BG_CARD, bordercolor=BORDER,
            troughcolor=BG_PANEL, font=("Segoe UI", 10))

        style.configure("TFrame", background=BG_APP)
        style.configure("Card.TFrame", background=BG_CARD, relief="flat")

        style.configure("TLabel", background=BG_APP, foreground=TEXT_PRI)
        style.configure("Muted.TLabel", background=BG_APP, foreground=TEXT_HINT)
        style.configure("Header.TLabel", background=BG_APP,
                        foreground=TEXT_PRI, font=("Segoe UI", 11, "bold"))

        style.configure("TButton",
            background=BG_CARD, foreground=TEXT_PRI,
            bordercolor=BORDER, focuscolor=ACCENT, relief="flat", padding=(10, 6))
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

        style.configure("TNotebook", background=BG_PANEL, bordercolor=BORDER)
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
        style.configure("TLabelframe", background=BG_APP, bordercolor=BORDER)
        style.configure("TLabelframe.Label",
            background=BG_APP, foreground=TEXT_MUT,
            font=("Segoe UI", 10, "bold"))

    def _configure_style(self):
        style = ttk.Style(self)
        style.theme_use("clam")
        self.configure(bg="#eef2ff")
        style.configure(".", font=("Segoe UI", 10))
        style.configure("TLabelframe", padding=8)
        style.configure("TLabelframe", background="#f8faff")
        style.configure("TLabelframe.Label", font=("Segoe UI", 10, "bold"))
        style.configure("TNotebook", tabposition="n")
        style.configure("TButton", padding=(10, 6))
        style.configure("Header.TLabel", font=("Segoe UI", 11, "bold"))

    def _build_controls(self, root: ttk.Frame):
        # Заголовок-разделитель
        hdr = ttk.Frame(root, style="TFrame")
        hdr.pack(fill=tk.X, pady=(0, 2))
        ttk.Label(hdr, text="Параметры запуска", style="Muted.TLabel").pack(side=tk.LEFT)
        ttk.Separator(hdr, orient="horizontal").pack(fill=tk.X, expand=True, padx=(8,0), pady=5)

        controls = ttk.Frame(root, style="TFrame")
        controls.pack(fill=tk.X, pady=(0, 8))
        controls.columnconfigure(1, weight=1)

        def make_row(parent, label_text, row):
            ttk.Label(parent, text=label_text, style="Muted.TLabel").grid(
                row=row, column=0, padx=(0,8), pady=4, sticky="w")

        make_row(controls, "Входной файл:", 0)
        ttk.Entry(controls, textvariable=self.input_file).grid(
            row=0, column=1, pady=4, sticky="ew")
        ttk.Button(controls, text="Обзор", command=self.pick_input).grid(
            row=0, column=2, padx=(8,0), pady=4)

        make_row(controls, "Выходная папка:", 1)
        ttk.Entry(controls, textvariable=self.output_dir).grid(
            row=1, column=1, pady=4, sticky="ew")
        ttk.Button(controls, text="Обзор", command=self.pick_output).grid(
            row=1, column=2, padx=(8,0), pady=4)

        make_row(controls, "Алгоритм:", 2)
        ttk.Combobox(controls, textvariable=self.algorithm,
                    values=["left", "yoshimura"],
                    state="readonly", width=20).grid(
            row=2, column=1, pady=4, sticky="w")
        ttk.Button(controls, text="▶  Запустить",
                command=self.run_backend_and_open,
                style="Accent.TButton").grid(
            row=2, column=2, padx=(8,0), pady=4)

    def _build_views(self, root: ttk.Frame):
        actions = ttk.Frame(root, bg=BG_APP)
        actions.pack(fill=tk.X, pady=8)
        ttk.Button(actions, text="Clear", command=self.clear).pack(side=tk.LEFT, padx=4)

        self.notebook = ttk.Notebook(root)
        self.notebook.pack(fill=tk.BOTH, expand=True)

        summary_frame = ttk.Frame(self.notebook, padding=8, bg=BG_CARD)
        chart_frame = ttk.Frame(self.notebook, padding=8, bg=BG_CARD)
        map_frame = ttk.Frame(self.notebook, padding=8, bg=BG_CARD)
        
        self.metric_bar = tk.Frame(summary_frame, bg=BG_APP)
        self.metric_bar.pack(fill=tk.X, pady=(0, 10))

        # 4 карточки
        self.m_tracks  = self._make_metric_card(self.metric_bar, "Треков", "—")
        self.m_nets    = self._make_metric_card(self.metric_bar, "Цепей", "—")
        self.m_wire    = self._make_metric_card(self.metric_bar, "Длина проводов", "—")
        self.m_conf    = self._make_metric_card(self.metric_bar, "Конфликты", "—")
        for w in (self.m_tracks, self.m_nets, self.m_wire, self.m_conf):
            w["frame"].pack(side=tk.LEFT, expand=True, fill=tk.X, padx=(0, 8))

        self.notebook.add(summary_frame, text="Summary")
        self.notebook.add(chart_frame, text="Charts")
        self.notebook.add(map_frame, text="Routing map")

        self.summary_text = tk.Text(summary_frame, wrap=tk.WORD,
            font=("Segoe UI", 11), relief=tk.FLAT,
            bg=BG_CARD, fg=TEXT_MUT, insertbackground=TEXT_PRI,
            bd=0, highlightthickness=1, highlightbackground=BORDER,
            state=tk.DISABLED)
        
        summary_scroll = ttk.Scrollbar(summary_frame, orient="vertical", command=self.summary_text.yview)
        self.summary_text.configure(yscrollcommand=summary_scroll.set)
        self.summary_text.pack(side=tk.LEFT, fill=tk.BOTH, expand=True)
        summary_scroll.pack(side=tk.RIGHT, fill=tk.Y)

        chart_container = ttk.Frame(chart_frame, bg=BG_APP)
        chart_container.pack(fill=tk.BOTH, expand=True)
        self.chart_canvas = tk.Canvas(chart_container, bg=BG_APP, highlightthickness=0)
        chart_scroll_y = ttk.Scrollbar(chart_container, orient="vertical", command=self.chart_canvas.yview)
        chart_scroll_x = ttk.Scrollbar(chart_container, orient="horizontal", command=self.chart_canvas.xview)
        self.chart_canvas.configure(xscrollcommand=chart_scroll_x.set, yscrollcommand=chart_scroll_y.set, bg=BG_APP)
        self.chart_canvas.grid(row=0, column=0, sticky="nsew", bg=BG_APP)
        chart_scroll_y.grid(row=0, column=1, sticky="ns")
        chart_scroll_x.grid(row=1, column=0, sticky="ew")
        chart_container.rowconfigure(0, weight=1)
        chart_container.columnconfigure(0, weight=1)

        map_toolbar = ttk.Frame(map_frame, bg=BG_APP)
        map_toolbar.pack(fill=tk.X, pady=(0, 8))
        ttk.Label(map_toolbar, text="Segment page:", style="Header.TLabel").pack(side=tk.LEFT)
        self.prev_button = ttk.Button(map_toolbar, text="◀ Prev", command=self.prev_page)
        self.prev_button.pack(side=tk.LEFT, padx=(8, 4))
        self.next_button = ttk.Button(map_toolbar, text="Next ▶", command=self.next_page)
        self.next_button.pack(side=tk.LEFT, padx=4)
        self.page_label = ttk.Label(map_toolbar, text="No data", style="Header.TLabel")
        self.page_label.pack(side=tk.LEFT, padx=12)
        ttk.Button(map_toolbar, text="−", width=3, command=lambda: self.change_zoom(0.85)).pack(side=tk.LEFT, padx=(8, 2))
        ttk.Button(map_toolbar, text="+", width=3, command=lambda: self.change_zoom(1.15)).pack(side=tk.LEFT, padx=2)
        self.zoom_label = ttk.Label(map_toolbar, text="100%")
        self.zoom_label.pack(side=tk.LEFT, padx=(4, 0))

        self.map_stats = ttk.Label(map_toolbar, text="", foreground="#445")
        self.map_stats.pack(side=tk.RIGHT)

        map_container = ttk.Frame(map_frame, bg=BG_APP)
        map_container.pack(fill=tk.BOTH, expand=True)
        self.map_canvas = tk.Canvas(map_container, bg=BG_CARD, highlightthickness=0)
        map_scroll_y = ttk.Scrollbar(map_container, orient="vertical", command=self.map_canvas.yview)
        map_scroll_x = ttk.Scrollbar(map_container, orient="horizontal", command=self.map_canvas.xview)
        self.map_canvas.configure(xscrollcommand=map_scroll_x.set, yscrollcommand=map_scroll_y.set)
        self.map_canvas.bind("<Control-MouseWheel>", self.on_zoom_mousewheel)
        self.map_canvas.grid(row=0, column=0, sticky="nsew")
        map_scroll_y.grid(row=0, column=1, sticky="ns")
        map_scroll_x.grid(row=1, column=0, sticky="ew")
        map_container.rowconfigure(0, weight=1)
        map_container.columnconfigure(0, weight=1)
        
        chart_toolbar = tk.Frame(chart_frame, bg=BG_APP)
        chart_toolbar.pack(fill=tk.X, pady=(0, 8))

        tk.Label(chart_toolbar, text="Режим:", bg=BG_APP,
                fg=TEXT_HINT, font=("Segoe UI", 10)).pack(side=tk.LEFT)
        self.btn_detail = tk.Button(chart_toolbar, text="Детальный",
            bg=ACCENT, fg="#ffffff", relief="flat", bd=0,
            font=("Segoe UI", 10), padx=12, pady=4,
            command=lambda: self._set_chart_mode("detail"))
        self.btn_detail.pack(side=tk.LEFT, padx=(6, 0))
        self.btn_agg = tk.Button(chart_toolbar, text="Агрегированный",
            bg=BG_CARD, fg=TEXT_MUT, relief="flat", bd=0,
            font=("Segoe UI", 10), padx=12, pady=4,
            command=lambda: self._set_chart_mode("agg"))
        self.btn_agg.pack(side=tk.LEFT, padx=(4, 0))
        self.chart_mode = "detail"

    def _set_chart_mode(self, mode: str):
        self.chart_mode = mode
        self.btn_detail.config(bg=ACCENT if mode=="detail" else BG_CARD,
                            fg="#ffffff" if mode=="detail" else TEXT_MUT)
        self.btn_agg.config(bg=ACCENT if mode=="agg" else BG_CARD,
                            fg="#ffffff" if mode=="agg" else TEXT_MUT)
        if self.current_data:
            self.render_charts(self.current_data)
    
    def _make_metric_card(self, parent, label: str, value: str) -> dict:
        f = tk.Frame(parent, bg=BG_CARD, bd=0,
                    highlightthickness=1, highlightbackground=BORDER)
        tk.Label(f, text=label, bg=BG_CARD, fg=TEXT_HINT,
                font=("Segoe UI", 9)).pack(anchor="w", padx=12, pady=(10,0))
        val_lbl = tk.Label(f, text=value, bg=BG_CARD, fg=ACCENT,
                        font=("Segoe UI", 20, "bold"))
        val_lbl.pack(anchor="w", padx=12, pady=(2, 10))
        return {"frame": f, "label": val_lbl}
    
    
    def pick_input(self):
        path = filedialog.askopenfilename(filetypes=[("Text", "*.txt"), ("All files", "*.*")])
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
        ch  = data.get("channel", {})
        rs  = data.get("result", {})
        segs = rs.get("segments", [])
        h_count = sum(1 for s in segs if s.get("type", 0) == 0)
        v_count = len(segs) - h_count
        conflicts = rs.get("conflictCount", 0)

        self.m_tracks["label"].config(text=str(rs.get("tracksUsed", "—")), fg=ACCENT)
        self.m_nets["label"].config(text=str(ch.get("nets", "—")), fg=TEXT_PRI)
        self.m_wire["label"].config(text=f'{rs.get("wireLength", 0):.0f}', fg=TEXT_PRI)
        conf_color = COL_RED if conflicts > 0 else COL_GREEN
        self.m_conf["label"].config(text=str(conflicts), fg=conf_color)

        # Детальный блок
        lines = [
            f"Алгоритм:          {data.get('algorithmName', '')}",
            f"Сохранено:         {data.get('savedAt', '')}",
            f"Ширина канала:     {ch.get('width', 0)}",
            f"Время выполнения:  {rs.get('executionMs', 0):.3f} ms",
            f"Сегментов всего:   {len(segs)}  "
            f"(горизонтальных: {h_count}, вертикальных: {v_count})",
        ]
        self.summary_text.config(state=tk.NORMAL)
        self.summary_text.delete("1.0", tk.END)
        self.summary_text.insert(tk.END, "\n".join(lines))
        self.summary_text.config(state=tk.DISABLED)

    def render_charts(self, data: dict):
        rs = data.get("result", {})
        segs = rs.get("segments", [])
        tracks = rs.get("tracksUsed", 1) or 1
        self.chart_canvas.delete("all")

        cw = max(self.chart_canvas.winfo_width(), 900)
        ch_h = max(self.chart_canvas.winfo_height(), 500)

        # --- Секция 1: состав сегментов ---
        h_count = sum(1 for s in segs if s.get("type", 0) == 0)
        v_count = len(segs) - h_count
        total = max(1, len(segs))
        x0, y0 = 40, 30
        bar_w = min(500, cw - 80)

        self.chart_canvas.create_text(x0, y0 - 14, text="Состав сегментов",
            anchor="w", font=("Segoe UI", 11, "bold"), fill=TEXT_PRI)
        # Фон
        self.chart_canvas.create_rectangle(x0, y0, x0+bar_w, y0+24,
            fill=BG_CARD, outline=BORDER)
        # Горизонтальный
        hw = int(bar_w * h_count / total)
        self.chart_canvas.create_rectangle(x0, y0, x0+hw, y0+24,
            fill=ACCENT, outline="")
        # Вертикальный
        self.chart_canvas.create_rectangle(x0+hw, y0, x0+bar_w, y0+24,
            fill=ACCENT2, outline="")
        self.chart_canvas.create_text(x0, y0+36,
            text=f"● Горизонтальные: {h_count} ({100*h_count//total}%)",
            anchor="w", fill=ACCENT, font=("Segoe UI", 10))
        self.chart_canvas.create_text(x0+260, y0+36,
            text=f"● Вертикальные: {v_count} ({100*v_count//total}%)",
            anchor="w", fill=ACCENT2, font=("Segoe UI", 10))

        # --- Секция 2: гистограмма ---
        hist_top = y0 + 64
        hist_h = 260
        hist_left = 50
        hist_bot = hist_top + hist_h

        self.chart_canvas.create_text(x0, hist_top - 14,
            text="Занятость треков" + (" (агрегировано)" if self.chart_mode == "agg" else ""),
            anchor="w", font=("Segoe UI", 11, "bold"), fill=TEXT_PRI)

        # Подготовить данные
        from collections import Counter
        occ = Counter(s.get("track", 0) for s in segs if s.get("type", 0) == 0)

        if self.chart_mode == "detail":
            bar_data = [occ.get(t, 0) for t in range(tracks)]
            bar_labels = list(range(tracks))
        else:
            bucket = max(1, tracks // 80)
            groups = []
            labels = []
            for i in range(0, tracks, bucket):
                groups.append(max(occ.get(t, 0) for t in range(i, min(tracks, i+bucket))))
                labels.append(str(i))
            bar_data = groups
            bar_labels = labels

        if not bar_data:
            self.chart_canvas.config(scrollregion=(0, 0, cw, ch_h))
            return

        max_val = max(bar_data) or 1
        n_bars = len(bar_data)
        chart_w = max(cw - 100, n_bars * 6)
        bar_unit = chart_w / n_bars

        # Горизонтальные линии сетки (4 уровня)
        for frac, label in [(1.0, str(max_val)), (0.75, str(int(max_val*0.75))),
                            (0.5, str(int(max_val*0.5))), (0.25, str(int(max_val*0.25))),
                            (0.0, "0")]:
            gy = hist_bot - int(hist_h * frac)
            self.chart_canvas.create_line(hist_left, gy, hist_left + chart_w, gy,
                fill=BORDER, dash=(4, 4))
            self.chart_canvas.create_text(hist_left - 4, gy, text=label,
                anchor="e", fill=TEXT_HINT, font=("Segoe UI", 9))

        # Столбцы
        for i, val in enumerate(bar_data):
            bh = int(hist_h * val / max_val)
            x1 = hist_left + i * bar_unit
            x2 = x1 + max(1, bar_unit - 1)
            y1 = hist_bot - bh
            color = BAR_COLORS[i % len(BAR_COLORS)]
            self.chart_canvas.create_rectangle(x1, y1, x2, hist_bot,
                fill=color, outline="")
            # Метки оси X — каждый ~80px
            if n_bars <= 40 or i % max(1, n_bars // 20) == 0:
                self.chart_canvas.create_text(
                    x1 + bar_unit/2, hist_bot + 12,
                    text=str(bar_labels[i]),
                    fill=TEXT_HINT, font=("Segoe UI", 8), anchor="n")

        # Рамка
        self.chart_canvas.create_rectangle(hist_left, hist_top,
            hist_left + chart_w, hist_bot, outline=BORDER)

        total_h = hist_bot + 60
        self.chart_canvas.config(
            scrollregion=(0, 0, hist_left + chart_w + 60, total_h),
            bg=BG_APP)

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
        track_h = max(12, int(20 * self.map_zoom))
        scale = max(3, int(8 * self.map_zoom))
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
            self.map_canvas.create_line(x1, y, x2, y, fill=color, width=max(1, int(track_h * 0.3)))

        self.map_canvas.create_rectangle(margin_x, margin_y, margin_x + drawable_w, margin_y + drawable_h, outline="#94a3b8")
        self.map_canvas.create_text(margin_x, 16, text="Routing map by segment pages (horizontal only)", anchor="w", font=("Segoe UI", 12, "bold"))
        self.map_canvas.config(scrollregion=(0, 0, margin_x + drawable_w + 40, margin_y + drawable_h + 40))
        self.zoom_label.config(text=f"{int(self.map_zoom * 100)}%")

    def change_zoom(self, factor: float):
        self.map_zoom = max(0.4, min(2.8, self.map_zoom * factor))
        self.render_current_page()

    def on_zoom_mousewheel(self, event):
        self.change_zoom(1.12 if event.delta > 0 else 0.88)

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
        self.map_zoom = 1.0
        self.summary_text.delete("1.0", tk.END)
        self.chart_canvas.delete("all")
        self.map_canvas.delete("all")
        self.page_label.config(text="No data")
        self.map_stats.config(text="")
        self.zoom_label.config(text="100%")


if __name__ == "__main__":
    App().mainloop()
