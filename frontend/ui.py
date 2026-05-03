import json
import tkinter as tk
from tkinter import filedialog, ttk


def render(data: dict) -> str:
    ch = data.get("channel", {})
    rs = data.get("result", {})
    lines = [
        f"Algorithm: {data.get('algorithmName','')}",
        f"Saved at: {data.get('savedAt','')}",
        f"Width: {ch.get('width',0)}  Nets: {ch.get('nets',0)}",
        f"Tracks used: {rs.get('tracksUsed',0)}",
        f"Wire length: {rs.get('wireLength',0)}",
        f"Conflicts: {rs.get('conflictCount',0)}",
        f"Execution ms: {rs.get('executionMs',0):.3f}",
        "",
        "Segments (first 200):"
    ]
    for s in rs.get("segments", [])[:200]:
        t = "H" if s.get("type", 0) == 0 else "V"
        lines.append(f"net={s.get('netId')} {t} [{s.get('start')}-{s.get('end')}] track={s.get('track')}")
    return "\n".join(lines)


class App(tk.Tk):
    def __init__(self):
        super().__init__()
        self.title("Channel Tracing Viewer")
        self.geometry("900x600")
        frame = ttk.Frame(self)
        frame.pack(fill=tk.BOTH, expand=True, padx=8, pady=8)

        btns = ttk.Frame(frame)
        btns.pack(fill=tk.X)
        ttk.Button(btns, text="Open Result JSON", command=self.open_file).pack(side=tk.LEFT, padx=4)
        ttk.Button(btns, text="Clear", command=self.clear).pack(side=tk.LEFT, padx=4)

        self.text = tk.Text(frame, wrap=tk.NONE, font=("Consolas", 11))
        self.text.pack(fill=tk.BOTH, expand=True, pady=8)

    def open_file(self):
        path = filedialog.askopenfilename(filetypes=[("JSON", "*.json"), ("All files", "*.*")])
        if not path:
            return
        with open(path, "r", encoding="utf-8") as f:
            data = json.load(f)
        self.text.delete("1.0", tk.END)
        self.text.insert(tk.END, render(data))

    def clear(self):
        self.text.delete("1.0", tk.END)


if __name__ == "__main__":
    App().mainloop()
