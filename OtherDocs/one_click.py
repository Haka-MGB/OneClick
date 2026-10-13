#!/usr/bin/env python3
"""One Click 0.1 - system utility toolkit. Run: python one_click.py (Python 3.8+, no packages needed)."""
import json, os, re, shutil, sys, threading
from datetime import datetime
from pathlib import Path
import tkinter as tk
from tkinter import ttk, filedialog, messagebox

APP, HOME = "One Click", Path.home()
DATA = HOME / ".oneclick"
HOLD, JOURNAL = DATA / "holding", DATA / "history.json"
NAVY, NAVY2, ACCENT = "#14213D", "#1F3158", "#2F6FED"
BG, CARD, INK, MUTED, LINE, DIM = "#F4F6F9", "#FFFFFF", "#1B2433", "#6B7686", "#DDE2EA", "#9FADC8"
FONT = "Segoe UI" if sys.platform == "win32" else ("Helvetica Neue" if sys.platform == "darwin" else "DejaVu Sans")

# ---------------------------------------------------------------- engine
PROTECTED = [Path(p).resolve() for p in filter(None, [
    os.environ.get("SystemRoot"), os.environ.get("ProgramFiles"), os.environ.get("ProgramFiles(x86)"),
    "/usr", "/bin", "/sbin", "/etc", "/boot", "/System", "/Library"])]

def is_protected(path):
    p = Path(path).resolve()
    return p.parent == p or p == HOME or any(p == q or q in p.parents for q in PROTECTED)

def unique(dst):
    dst, i = Path(dst), 1
    while dst.exists():
        dst = dst.with_name(f"{Path(dst).stem.rsplit(' (', 1)[0]} ({i}){dst.suffix}")
        i += 1
    return dst

def human(n):
    for u in ("B", "KB", "MB", "GB", "TB"):
        if n < 1024 or u == "TB":
            return f"{n:.0f} B" if u == "B" else f"{n:.1f} {u}"
        n /= 1024

class Journal:
    """Every change the app makes is a list of moves, so one undo mechanism covers all tools."""
    def __init__(self):
        try: self.items = json.loads(JOURNAL.read_text("utf-8"))
        except Exception: self.items = []
    def _save(self):
        DATA.mkdir(exist_ok=True)
        JOURNAL.write_text(json.dumps(self.items[-50:]), "utf-8")
    def apply(self, label, moves):
        done = []
        for src, dst in moves:
            try:
                dst = unique(dst); Path(dst).parent.mkdir(parents=True, exist_ok=True)
                shutil.move(str(src), str(dst)); done.append([str(src), str(dst)])
            except OSError:
                pass
        if done:
            self.items.append({"label": label, "time": datetime.now().strftime("%d %b %Y, %H:%M"), "moves": done})
            self._save()
        return len(done), len(moves) - len(done)
    def undo(self):
        if not self.items: return None
        op, back = self.items.pop(), 0
        for src, dst in reversed(op["moves"]):
            try:
                if Path(dst).exists():
                    s = unique(src); s.parent.mkdir(parents=True, exist_ok=True)
                    shutil.move(dst, str(s)); back += 1
            except OSError:
                pass
        self._save()
        return op["label"], back
    def last(self): return self.items[-1]["label"] if self.items else None

def plan_rename(folder, find, repl, prefix, suffix, case, number, start):
    files = sorted(f for f in Path(folder).iterdir() if f.is_file() and not f.name.startswith("."))
    moves, n = [], start
    for f in files:
        stem = f.stem.replace(find, repl) if find else f.stem
        stem = {1: stem.lower(), 2: stem.upper(), 3: stem.title()}.get(case, stem)
        stem = re.sub(r'[<>:"/\\|?*]', "", f"{prefix}{stem}{suffix}")
        if number: stem, n = f"{stem}_{n:03d}", n + 1
        if stem and stem + f.suffix != f.name: moves.append((f, f.with_name(stem + f.suffix)))
    return moves, len(files)

CATS = {
    "Images": {".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp", ".svg", ".heic"},
    "Documents": {".pdf", ".doc", ".docx", ".txt", ".rtf", ".odt", ".ppt", ".pptx", ".md"},
    "Spreadsheets": {".xls", ".xlsx", ".csv", ".ods"},
    "Video": {".mp4", ".mkv", ".mov", ".avi", ".wmv", ".webm"},
    "Audio": {".mp3", ".wav", ".flac", ".m4a", ".aac", ".ogg"},
    "Archives": {".zip", ".rar", ".7z", ".tar", ".gz"},
    "Installers": {".exe", ".msi", ".dmg", ".pkg", ".deb", ".apk"},
    "Code": {".py", ".js", ".html", ".css", ".java", ".cs", ".json", ".cpp", ".c", ".sql"},
}

def plan_organise(folder, mode):
    moves = []
    for f in sorted(Path(folder).iterdir()):
        if not f.is_file() or f.name.startswith("."): continue
        if mode == 0:
            sub = next((k for k, v in CATS.items() if f.suffix.lower() in v), "Other")
        else:
            sub = datetime.fromtimestamp(f.stat().st_mtime).strftime("%Y-%m")
        moves.append((f, Path(folder) / sub / f.name))
    return moves

def scan_files(folder, min_bytes, limit=300):
    found = []
    for root, dirs, files in os.walk(folder):
        dirs[:] = [d for d in dirs if not d.startswith(".")]
        if is_protected(root): dirs[:] = []; continue
        for name in files:
            p = os.path.join(root, name)
            try:
                st = os.stat(p)
                if st.st_size >= min_bytes: found.append((st.st_size, st.st_mtime, p))
            except OSError:
                pass
    return sorted(found, reverse=True)[:limit]

# ---------------------------------------------------------------- ui kit
def style(root):
    s = ttk.Style(root); s.theme_use("clam")
    s.configure(".", font=(FONT, 10))
    s.configure("TButton", padding=(14, 7), background="#E7EBF2", foreground=INK, borderwidth=0, focusthickness=0)
    s.map("TButton", background=[("active", "#D9DFEA"), ("disabled", "#EEF0F4")])
    s.configure("Accent.TButton", background=ACCENT, foreground="white")
    s.map("Accent.TButton", background=[("active", "#2559C4"), ("disabled", "#9DB7F0")])
    s.configure("Side.TButton", background=NAVY2, foreground="white")
    s.map("Side.TButton", background=[("active", "#2A4172"), ("disabled", NAVY2)], foreground=[("disabled", DIM)])
    s.configure("Treeview", rowheight=28, background=CARD, fieldbackground=CARD, foreground=INK, borderwidth=0)
    s.configure("Treeview.Heading", background="#EEF1F6", foreground=MUTED, font=(FONT, 9, "bold"), padding=(8, 6), relief="flat")
    s.map("Treeview", background=[("selected", "#DCE7FD")], foreground=[("selected", INK)])
    s.configure("TEntry", padding=6, fieldbackground="white")
    s.configure("TCombobox", padding=5, fieldbackground="white")
    for w in ("TCheckbutton", "TRadiobutton"): s.configure(w, background=CARD, foreground=INK)

def card(parent): return tk.Frame(parent, bg=CARD, highlightbackground=LINE, highlightthickness=1)
def label(parent, text, size=10, bold=False, fg=INK, bg=CARD, **kw):
    return tk.Label(parent, text=text, font=(FONT, size, "bold" if bold else "normal"), fg=fg, bg=bg, **kw)

def table(parent, cols):
    f = tk.Frame(parent, bg=CARD)
    t = ttk.Treeview(f, columns=[c[0] for c in cols], show="headings", selectmode="extended")
    for cid, text, w in cols:
        t.heading(cid, text=text); t.column(cid, width=w, anchor="w")
    sb = ttk.Scrollbar(f, command=t.yview); t.configure(yscrollcommand=sb.set)
    t.pack(side="left", fill="both", expand=True); sb.pack(side="right", fill="y")
    return f, t

class FolderBar(tk.Frame):
    def __init__(self, parent, callback):
        super().__init__(parent, bg=CARD)
        dl = HOME / "Downloads"
        self.var = tk.StringVar(value=str(dl if dl.is_dir() else HOME))
        label(self, "Folder", 10, True).pack(side="left", padx=(0, 12))
        ttk.Entry(self, textvariable=self.var).pack(side="left", fill="x", expand=True)
        ttk.Button(self, text="Browse", command=self.pick).pack(side="left", padx=(8, 0))
        self.var.trace_add("write", lambda *a: callback())
    def pick(self):
        d = filedialog.askdirectory(initialdir=self.var.get())
        if d: self.var.set(d)
    def path(self):
        p = Path(self.var.get()).expanduser()
        return p if p.is_dir() else None

class Page(tk.Frame):
    title, sub = "", ""
    def __init__(self, app):
        super().__init__(app.body, bg=BG); self.app = app
        head = tk.Frame(self, bg=BG); head.pack(fill="x", padx=32, pady=(26, 14))
        label(head, self.title, 20, True, bg=BG).pack(anchor="w")
        label(head, self.sub, 10, fg=MUTED, bg=BG).pack(anchor="w", pady=(3, 0))
        self.main = tk.Frame(self, bg=BG); self.main.pack(fill="both", expand=True, padx=32, pady=(0, 20))
    def refresh(self): pass
    def folder_card(self, callback):
        c = card(self.main); c.pack(fill="x", pady=(0, 14))
        self.fb = FolderBar(c, callback); self.fb.pack(fill="x", padx=18, pady=14)

# ---------------------------------------------------------------- pages
class HomePage(Page):
    title, sub = "Overview", "The state of this computer and the tools to look after it."
    def __init__(self, app):
        super().__init__(app)
        self.disk = card(self.main); self.disk.pack(fill="x", pady=(0, 14))
        tools = tk.Frame(self.main, bg=BG); tools.pack(fill="x", pady=(0, 14))
        info = [("rename", "Bulk Rename", "Rename hundreds of files with simple rules and a live preview."),
                ("organise", "Folder Organiser", "Sort a cluttered folder into tidy groups by type or month."),
                ("storage", "Storage Analyser", "Find the largest files and move them out of the way safely.")]
        for i, (key, name, desc) in enumerate(info):
            tools.columnconfigure(i, weight=1, uniform="t")
            c = card(tools); c.grid(row=0, column=i, sticky="nsew", padx=(0 if i == 0 else 7, 0 if i == 2 else 7))
            label(c, name, 12, True).pack(anchor="w", padx=18, pady=(16, 4))
            label(c, desc, 10, fg=MUTED, wraplength=230, justify="left").pack(anchor="w", padx=18)
            ttk.Button(c, text="Open", command=lambda k=key: app.show(k)).pack(anchor="w", padx=18, pady=16)
        self.log = card(self.main); self.log.pack(fill="both", expand=True)
    def refresh(self):
        for w in self.disk.winfo_children() + self.log.winfo_children(): w.destroy()
        root = HOME.anchor or "/"
        u = shutil.disk_usage(root); pct = u.used / u.total * 100
        label(self.disk, "Disk usage", 12, True).pack(anchor="w", padx=18, pady=(16, 2))
        label(self.disk, f"{human(u.used)} used of {human(u.total)} on {root}  ({pct:.0f}% full)", 10, fg=MUTED).pack(anchor="w", padx=18)
        bar = tk.Canvas(self.disk, height=10, bg="#E6EAF1", highlightthickness=0); bar.pack(fill="x", padx=18, pady=10)
        bar.bind("<Configure>", lambda e: (bar.delete("all"), bar.create_rectangle(
            0, 0, e.width * pct / 100, 10, fill="#C2410C" if pct >= 90 else ACCENT, width=0)))
        msg = ("This drive is almost full. A full drive slows the whole computer. Use Storage Analyser to free space." if pct >= 90
               else "Space is getting tight. Consider reviewing large files soon." if pct >= 75
               else "There is plenty of free space.")
        label(self.disk, msg, 10).pack(anchor="w", padx=18, pady=(0, 16))
        label(self.log, "Recent activity", 12, True).pack(anchor="w", padx=18, pady=(16, 6))
        items = self.app.journal.items[-6:][::-1]
        if not items: label(self.log, "Nothing yet. Actions you apply will be listed here and can be undone.", 10, fg=MUTED).pack(anchor="w", padx=18)
        for it in items:
            r = tk.Frame(self.log, bg=CARD); r.pack(fill="x", padx=18, pady=3)
            label(r, it["label"], 10).pack(side="left")
            label(r, f'{len(it["moves"])} files   {it["time"]}', 9, fg=MUTED).pack(side="right")

class RenamePage(Page):
    title, sub = "Bulk Rename", "Rename every file in a folder with simple rules. Check the preview, then apply. Undo is always available."
    def __init__(self, app):
        super().__init__(app)
        self.moves = []
        self.folder_card(self.update_preview)
        row = tk.Frame(self.main, bg=BG); row.pack(fill="both", expand=True)
        left = card(row); left.pack(side="left", fill="y", padx=(0, 14))
        form = tk.Frame(left, bg=CARD); form.pack(padx=18, pady=16)
        self.v = {k: tk.StringVar() for k in ("find", "repl", "prefix", "suffix")}
        self.case, self.num, self.start = tk.StringVar(value="Keep as is"), tk.BooleanVar(), tk.StringVar(value="1")
        for i, (k, t) in enumerate([("find", "Find text"), ("repl", "Replace with"), ("prefix", "Add prefix"), ("suffix", "Add suffix")]):
            label(form, t, 9, fg=MUTED).grid(row=i * 2, column=0, sticky="w", pady=(8, 2))
            ttk.Entry(form, textvariable=self.v[k], width=26).grid(row=i * 2 + 1, column=0, sticky="we")
        label(form, "Letter case", 9, fg=MUTED).grid(row=8, column=0, sticky="w", pady=(8, 2))
        self.cb = ttk.Combobox(form, textvariable=self.case, state="readonly", width=24,
                               values=["Keep as is", "lowercase", "UPPERCASE", "Title Case"])
        self.cb.grid(row=9, column=0, sticky="we")
        ttk.Checkbutton(form, text="Add sequence number", variable=self.num).grid(row=10, column=0, sticky="w", pady=(14, 2))
        label(form, "Start at", 9, fg=MUTED).grid(row=11, column=0, sticky="w", pady=(4, 2))
        ttk.Entry(form, textvariable=self.start, width=8).grid(row=12, column=0, sticky="w")
        right = card(row); right.pack(side="left", fill="both", expand=True)
        self.info = label(right, "", 10, fg=MUTED); self.info.pack(anchor="w", padx=18, pady=(14, 8))
        tf, self.tv = table(right, [("old", "Current name", 260), ("new", "New name", 260)])
        tf.pack(fill="both", expand=True, padx=1)
        self.go = ttk.Button(right, text="Apply rename", style="Accent.TButton", command=self.apply)
        self.go.pack(anchor="e", padx=18, pady=14)
        for var in (*self.v.values(), self.case, self.num, self.start): var.trace_add("write", lambda *a: self.update_preview())
        self.update_preview()
    def update_preview(self):
        self.tv.delete(*self.tv.get_children()); self.moves = []
        folder = self.app.safe(self.fb.path())
        if not folder: self.info.config(text="Choose a folder to begin."); self.go.state(["disabled"]); return
        try: start = int(self.start.get() or 1)
        except ValueError: start = 1
        self.moves, total = plan_rename(folder, self.v["find"].get(), self.v["repl"].get(), self.v["prefix"].get(),
                                        self.v["suffix"].get(), self.cb.current(), self.num.get(), start)
        for a, b in self.moves: self.tv.insert("", "end", values=(a.name, b.name))
        self.info.config(text=f"{len(self.moves)} of {total} files will change")
        self.go.state(["!disabled"] if self.moves else ["disabled"])
    def apply(self):
        self.app.run("Bulk rename", self.moves)
    def refresh(self): self.update_preview()

class OrganisePage(Page):
    title, sub = "Folder Organiser", "Sort loose files into tidy sub-folders. Nothing is deleted, and every move can be undone."
    def __init__(self, app):
        super().__init__(app)
        self.moves, self.mode = [], tk.IntVar(value=0)
        self.folder_card(self.update_preview)
        c = card(self.main); c.pack(fill="both", expand=True)
        top = tk.Frame(c, bg=CARD); top.pack(fill="x", padx=18, pady=(14, 8))
        label(top, "Group files", 10, True).pack(side="left", padx=(0, 14))
        for i, t in enumerate(("By file type", "By month last modified")):
            ttk.Radiobutton(top, text=t, value=i, variable=self.mode, command=self.update_preview).pack(side="left", padx=(0, 14))
        self.info = label(c, "", 10, fg=MUTED); self.info.pack(anchor="w", padx=18, pady=(0, 8))
        tf, self.tv = table(c, [("file", "File", 320), ("dest", "Will move to", 240)])
        tf.pack(fill="both", expand=True, padx=1)
        self.go = ttk.Button(c, text="Organise files", style="Accent.TButton", command=lambda: app.run("Folder organise", self.moves))
        self.go.pack(anchor="e", padx=18, pady=14)
        self.update_preview()
    def update_preview(self):
        self.tv.delete(*self.tv.get_children()); self.moves = []
        folder = self.app.safe(self.fb.path())
        if not folder: self.info.config(text="Choose a folder to begin."); self.go.state(["disabled"]); return
        self.moves = plan_organise(folder, self.mode.get())
        for a, b in self.moves: self.tv.insert("", "end", values=(a.name, b.parent.name))
        self.info.config(text=f"{len(self.moves)} files will be moved into sub-folders")
        self.go.state(["!disabled"] if self.moves else ["disabled"])
    def refresh(self): self.update_preview()

class StoragePage(Page):
    title, sub = "Storage Analyser", "Find the largest files in a folder. Selected files go to a holding area first, so nothing is lost by mistake."
    def __init__(self, app):
        super().__init__(app)
        self.result = None
        self.folder_card(lambda: None)
        c = card(self.main); c.pack(fill="both", expand=True)
        top = tk.Frame(c, bg=CARD); top.pack(fill="x", padx=18, pady=(14, 8))
        label(top, "Show files larger than", 10, True).pack(side="left", padx=(0, 10))
        self.size = ttk.Combobox(top, state="readonly", width=10, values=["Any size", "10 MB", "100 MB", "500 MB"])
        self.size.current(1); self.size.pack(side="left")
        self.scan_btn = ttk.Button(top, text="Scan", style="Accent.TButton", command=self.scan); self.scan_btn.pack(side="left", padx=10)
        self.info = label(top, "", 10, fg=MUTED); self.info.pack(side="left", padx=8)
        tf, self.tv = table(c, [("size", "Size", 90), ("date", "Modified", 110), ("path", "Location", 520)])
        tf.pack(fill="both", expand=True, padx=1)
        bar = tk.Frame(c, bg=CARD); bar.pack(fill="x", padx=18, pady=14)
        ttk.Button(bar, text="Move selected to holding area", style="Accent.TButton", command=self.clean).pack(side="left")
        ttk.Button(bar, text="Empty holding area", command=self.empty).pack(side="left", padx=10)
    def scan(self):
        folder = self.app.safe(self.fb.path())
        if not folder: return
        mb = [0, 10, 100, 500][self.size.current()]
        self.tv.delete(*self.tv.get_children()); self.scan_btn.state(["disabled"]); self.info.config(text="Scanning...")
        self.result = None
        threading.Thread(target=lambda: setattr(self, "result", scan_files(folder, mb * 1048576)), daemon=True).start()
        self.after(150, self.poll)
    def poll(self):
        if self.result is None: self.after(150, self.poll); return
        for size, mt, p in self.result:
            self.tv.insert("", "end", iid=p, values=(human(size), datetime.fromtimestamp(mt).strftime("%d %b %Y"), p))
        self.info.config(text=f"{len(self.result)} files, {human(sum(r[0] for r in self.result))} in total")
        self.scan_btn.state(["!disabled"])
    def clean(self):
        sel = self.tv.selection()
        if not sel: messagebox.showinfo(APP, "Select one or more files first."); return
        stamp = datetime.now().strftime("%Y%m%d-%H%M%S")
        self.app.run("Storage cleanup", [(p, HOLD / stamp / Path(p).name) for p in sel])
        for p in sel:
            if not Path(p).exists(): self.tv.delete(p)
    def empty(self):
        size = sum(f.stat().st_size for f in HOLD.rglob("*") if f.is_file()) if HOLD.exists() else 0
        if not size: messagebox.showinfo(APP, "The holding area is empty."); return
        if messagebox.askyesno(APP, f"Permanently delete {human(size)} from the holding area?\nThis cannot be undone."):
            shutil.rmtree(HOLD, ignore_errors=True); self.app.status(f"Freed {human(size)}.")

# ---------------------------------------------------------------- shell
class App(tk.Tk):
    def __init__(self):
        super().__init__()
        self.title(APP); self.geometry("1120x720"); self.minsize(980, 620); self.configure(bg=BG)
        style(self); self.journal = Journal(); self.nav, self.pages, self.current = {}, {}, None
        side = tk.Frame(self, bg=NAVY, width=232); side.pack(side="left", fill="y"); side.pack_propagate(False)
        brand = tk.Frame(side, bg=NAVY); brand.pack(fill="x", padx=20, pady=(26, 28))
        logo = tk.Canvas(brand, width=30, height=30, bg=NAVY, highlightthickness=0); logo.pack(side="left")
        logo.create_rectangle(0, 0, 30, 30, fill=ACCENT, width=0); logo.create_oval(9, 9, 21, 21, fill="white", width=0)
        t = tk.Frame(brand, bg=NAVY); t.pack(side="left", padx=12)
        label(t, APP, 14, True, "white", NAVY).pack(anchor="w")
        label(t, "Version 0.1", 8, fg=DIM, bg=NAVY).pack(anchor="w")
        self.body = tk.Frame(self, bg=BG); self.body.pack(side="right", fill="both", expand=True)
        self.bar = tk.Label(self.body, text="Ready", anchor="w", font=(FONT, 9), fg=MUTED, bg="#E9EDF3", padx=32, pady=6)
        self.bar.pack(side="bottom", fill="x")
        for key, text, cls in (("home", "Overview", HomePage), ("rename", "Bulk Rename", RenamePage),
                               ("organise", "Folder Organiser", OrganisePage), ("storage", "Storage Analyser", StoragePage)):
            self.add_nav(side, key, text); self.pages[key] = cls(self)
        foot = tk.Frame(side, bg=NAVY); foot.pack(side="bottom", fill="x", padx=20, pady=22)
        self.last = label(foot, "", 9, fg=DIM, bg=NAVY, wraplength=190, justify="left"); self.last.pack(anchor="w", pady=(0, 8))
        self.undo_btn = ttk.Button(foot, text="Undo last action", style="Side.TButton", command=self.undo); self.undo_btn.pack(fill="x")
        self.sync(); self.show("home")
    def add_nav(self, side, key, text):
        row = tk.Frame(side, bg=NAVY, cursor="hand2"); row.pack(fill="x")
        bar = tk.Frame(row, width=4, bg=NAVY); bar.pack(side="left", fill="y")
        lb = tk.Label(row, text=text, anchor="w", font=(FONT, 11), bg=NAVY, fg=DIM, padx=20, pady=12); lb.pack(side="left", fill="x", expand=True)
        for w in (row, lb): w.bind("<Button-1>", lambda e, k=key: self.show(k))
        self.nav[key] = (row, bar, lb)
    def show(self, key):
        if self.current: self.pages[self.current].pack_forget()
        self.current = key
        for k, (row, bar, lb) in self.nav.items():
            on = k == key
            for w in (row, lb): w.config(bg=NAVY2 if on else NAVY)
            bar.config(bg=ACCENT if on else NAVY); lb.config(fg="white" if on else DIM)
        self.pages[key].pack(fill="both", expand=True); self.pages[key].refresh()
    def status(self, msg): self.bar.config(text=msg)
    def sync(self):
        last = self.journal.last()
        self.last.config(text=f"Last action: {last}" if last else "No actions to undo")
        self.undo_btn.state(["!disabled"] if last else ["disabled"])
    def safe(self, folder):
        if folder and is_protected(folder):
            self.status("That folder is protected. One Click does not modify system folders or your home folder root.")
            return None
        return folder
    def run(self, label_, moves):
        if not moves: return
        sample = "\n".join(f"  {Path(a).name}" for a, _ in moves[:5])
        more = f"\n  ...and {len(moves) - 5} more" if len(moves) > 5 else ""
        if not messagebox.askyesno(APP, f"{label_}: {len(moves)} files will change.\n\n{sample}{more}\n\nContinue? You can undo this afterwards."): return
        ok, failed = self.journal.apply(label_, moves)
        self.status(f"{label_} complete: {ok} files changed" + (f", {failed} skipped." if failed else "."))
        self.after_change()
    def undo(self):
        res = self.journal.undo()
        if res: self.status(f"Undone: {res[0]} ({res[1]} files restored).")
        self.after_change()
    def after_change(self):
        self.sync(); self.pages[self.current].refresh()

if __name__ == "__main__":
    try:
        import ctypes; ctypes.windll.shcore.SetProcessDpiAwareness(1)
    except Exception:
        pass
    App().mainloop()
