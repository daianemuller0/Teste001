"""Interface do extrator de propostas."""
import os
import queue
import threading
import tkinter as tk
from tkinter import filedialog, messagebox, ttk

import excel_io
import extrator

BASE_PADRAO = r"H:\PV_AFTERMARKET"


class App(tk.Tk):
    def __init__(self):
        super().__init__()
        self.title("Extrator de Propostas")
        self.geometry("900x560")
        self.fila = queue.Queue()
        self.var_excel = tk.StringVar()
        self.var_base = tk.StringVar(value=BASE_PADRAO)
        self.var_linha = tk.IntVar(value=2)
        self._montar()
        self.after(100, self._consumir)

    def _montar(self):
        f = ttk.Frame(self, padding=10)
        f.pack(fill="x")
        ttk.Label(f, text="Planilha Excel:").grid(row=0, column=0, sticky="w")
        ttk.Entry(f, textvariable=self.var_excel).grid(row=0, column=1, sticky="ew", padx=5)
        ttk.Button(f, text="Procurar...", command=self._escolher_excel).grid(row=0, column=2)
        ttk.Label(f, text="Pasta dos pedidos:").grid(row=1, column=0, sticky="w", pady=5)
        ttk.Entry(f, textvariable=self.var_base).grid(row=1, column=1, sticky="ew", padx=5)
        ttk.Button(f, text="Procurar...", command=self._escolher_base).grid(row=1, column=2)
        ttk.Label(f, text="Primeira linha de dados:").grid(row=2, column=0, sticky="w")
        ttk.Spinbox(f, from_=1, to=100000, textvariable=self.var_linha, width=8).grid(row=2, column=1, sticky="w", padx=5)
        f.columnconfigure(1, weight=1)

        b = ttk.Frame(self, padding=(10, 0))
        b.pack(fill="x")
        self.btn = ttk.Button(b, text="Processar", command=self._iniciar)
        self.btn.pack(side="left")
        ttk.Button(b, text="Diagnóstico de PDF...", command=self._diagnostico).pack(side="left", padx=8)
        self.barra = ttk.Progressbar(self, mode="determinate")
        self.barra.pack(fill="x", padx=10, pady=8)

        cols = ("linha", "pedido", "status", "detalhe")
        self.tree = ttk.Treeview(self, columns=cols, show="headings")
        for c, t, w in zip(cols, ("Linha", "Pedido", "Status", "Detalhe"), (60, 110, 90, 600)):
            self.tree.heading(c, text=t)
            self.tree.column(c, width=w, anchor="w")
        self.tree.tag_configure("ok", foreground="#1a7f37")
        self.tree.tag_configure("aviso", foreground="#9a6700")
        self.tree.tag_configure("erro", foreground="#cf222e")
        self.tree.pack(fill="both", expand=True, padx=10, pady=(0, 10))
        self.lbl = ttk.Label(self, text="Pronto.")
        self.lbl.pack(anchor="w", padx=10, pady=(0, 8))

    def _escolher_excel(self):
        p = filedialog.askopenfilename(filetypes=[("Excel", "*.xlsx *.xlsm")])
        if p:
            self.var_excel.set(p)

    def _escolher_base(self):
        p = filedialog.askdirectory()
        if p:
            self.var_base.set(p)

    def _diagnostico(self):
        p = filedialog.askopenfilename(filetypes=[("PDF", "*.pdf")])
        if not p:
            return
        try:
            texto = extrator.ler_texto(p)
        except Exception as e:
            messagebox.showerror("Erro", str(e))
            return
        j = tk.Toplevel(self)
        j.title("Texto extraído do PDF")
        t = tk.Text(j, wrap="none", width=110, height=35)
        t.insert("1.0", texto)
        t.pack(fill="both", expand=True)

    def _iniciar(self):
        origem = self.var_excel.get().strip()
        if not os.path.isfile(origem):
            messagebox.showwarning("Atenção", "Selecione a planilha Excel.")
            return
        self.tree.delete(*self.tree.get_children())
        self.btn.state(["disabled"])
        args = (origem, self.var_base.get().strip(), self.var_linha.get())
        threading.Thread(target=self._trabalho, args=args, daemon=True).start()

    def _trabalho(self, origem, base, linha_ini):
        try:
            wb = excel_io.abrir(origem)
            ws = wb.active
            pedidos = list(excel_io.listar_pedidos(ws, linha_ini))
            self.fila.put(("total", len(pedidos)))
            ok = 0
            for linha, valor in pedidos:
                try:
                    dados, avisos, _ = extrator.processar_pedido(valor, base)
                    excel_io.gravar_linha(ws, linha, dados)
                    ok += 1
                    self.fila.put(("linha", linha, valor, "aviso" if avisos else "ok",
                                   "; ".join(avisos) or "OK"))
                except Exception as e:
                    self.fila.put(("linha", linha, valor, "erro", str(e)))
            saida = excel_io.caminho_saida(origem)
            wb.save(saida)
            self.fila.put(("fim", f"Concluído: {ok}/{len(pedidos)} preenchidos. Salvo em {saida}"))
        except Exception as e:
            self.fila.put(("fim", f"Erro: {e}"))

    def _consumir(self):
        try:
            while True:
                m = self.fila.get_nowait()
                if m[0] == "total":
                    self.barra.configure(maximum=max(m[1], 1), value=0)
                elif m[0] == "linha":
                    _, l, p, s, d = m
                    self.tree.insert("", "end", values=(l, p, s.upper(), d), tags=(s,))
                    self.barra.step(1)
                else:
                    self.lbl.configure(text=m[1])
                    self.btn.state(["!disabled"])
        except queue.Empty:
            pass
        self.after(100, self._consumir)


if __name__ == "__main__":
    App().mainloop()
