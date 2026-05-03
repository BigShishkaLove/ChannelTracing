import json
import os
import subprocess
import tkinter as tk
from tkinter import filedialog, ttk, messagebox


BACKEND_PROJECT = os.path.join("src", "src.csproj")


def render(data: dict) -> str:
    ch = data.get("channel", {})
    rs = data.get("result", {})
    lines = [
        f"Algorithm: {data.get('algorithmName', '')}",
        f"Saved at: {data.get('savedAt', '')}",
        f"Width: {ch.get('width', 0)}  Nets: {ch.get('nets', 0)}",
        f"Tracks used: {rs.get('tracksUsed', 0)}",
        f"Wire length: {rs.get('wireLength', 0)}",
        f"Conflicts: {rs.get('conflictCount', 0)}",
        f"Execution ms: {rs.get('executionMs', 0):.3f}",
        "",
        "Segments (first 200):",
    ]
    for s in rs.get("segments", [])[:200]:
        seg_type = "H" if s.get("type", 0) == 0 else "V"
        lines.append(
            f"net={s.get('netId')} {seg_type} [{s.get('start')}-{s.get('end')}] track={s.get('track')}"
        )
    return "\n".join(lines)


class App(tk.Tk):
    def __init__(self):
        super().__init__()
        self.title("Channel Tracing Viewer")
        self.geometry("980x680")

        self.input_file = tk.StringVar()
        self.output_dir = tk.StringVar(value="output")
        self.algorithm = tk.StringVar(value="both")

        root = ttk.Frame(self)
        root.pack(fill=tk.BOTH, expand=True, padx=8, pady=8)

        controls = ttk.LabelFrame(root, text="Backend Run")
        controls.pack(fill=tk.X)

        ttk.Label(controls, text="Input file:").grid(row=0, column=0, padx=4, pady=4, sticky="w")
        ttk.Entry(controls, textvariable=self.input_file, width=70).grid(row=0, column=1, padx=4, pady=4)
        ttk.Button(controls, text="Browse", command=self.pick_input).grid(row=0, column=2, padx=4, pady=4)

        ttk.Label(controls, text="Output dir:").grid(row=1, column=0, padx=4, pady=4, sticky="w")
        ttk.Entry(controls, textvariable=self.output_dir, width=70).grid(row=1, column=1, padx=4, pady=4)
        ttk.Button(controls, text="Browse", command=self.pick_output).grid(row=1, column=2, padx=4, pady=4)

        ttk.Label(controls, text="Algorithm:").grid(row=2, column=0, padx=4, pady=4, sticky="w")
        algo_box = ttk.Combobox(controls, textvariable=self.algorithm, values=["left", "yoshimura", "both"], state="readonly", width=20)
        algo_box.grid(row=2, column=1, padx=4, pady=4, sticky="w")

        ttk.Button(controls, text="Run backend + Open result", command=self.run_backend_and_open).grid(row=2, column=2, padx=4, pady=4)

        actions = ttk.Frame(root)
        actions.pack(fill=tk.X, pady=6)
        ttk.Button(actions, text="Open Result JSON", command=self.open_json).pack(side=tk.LEFT, padx=4)
        ttk.Button(actions, text="Clear", command=self.clear).pack(side=tk.LEFT, padx=4)

        self.text = tk.Text(root, wrap=tk.NONE, font=("Consolas", 11))
        self.text.pack(fill=tk.BOTH, expand=True)

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
        if not path:
            return
        self.show_json(path)

    def show_json(self, path: str):
        with open(path, "r", encoding="utf-8") as f:
            data = json.load(f)
        self.text.delete("1.0", tk.END)
        self.text.insert(tk.END, render(data))

    def run_backend_and_open(self):
        input_file = self.input_file.get().strip()
        output_dir = self.output_dir.get().strip() or "output"
        algo = self.algorithm.get().strip() or "both"

        if not input_file:
            messagebox.showerror("Input required", "Выберите входной файл канала.")
            return

        os.makedirs(output_dir, exist_ok=True)

        cmd = [
            "dotnet", "run", "--project", BACKEND_PROJECT,
            "--", input_file, output_dir, algo
        ]

        try:
            proc = subprocess.run(cmd, capture_output=True, text=True, check=False)
        except FileNotFoundError:
            messagebox.showerror("dotnet missing", "dotnet SDK не найден в PATH.")
            return

        if proc.returncode != 0:
            messagebox.showerror("Backend error", f"Код: {proc.returncode}\n\nSTDOUT:\n{proc.stdout}\n\nSTDERR:\n{proc.stderr}")
            return

        target = self.resolve_result_json(output_dir, algo)
        if target is None:
            messagebox.showwarning("No result", "Backend выполнен, но JSON результата не найден.")
            return

        self.show_json(target)

    @staticmethod
    def resolve_result_json(output_dir: str, algo: str) -> str | None:
        candidates = {
            "left": "left_edge_algorithm_latest.json",
            "yoshimura": "yoshimura_algorithm_latest.json",
            "both": "yoshimura_algorithm_latest.json",
        }
        file_name = candidates.get(algo, candidates["both"])
        path = os.path.join(output_dir, file_name)
        return path if os.path.exists(path) else None

    def clear(self):
        self.text.delete("1.0", tk.END)


if __name__ == "__main__":
    App().mainloop()
