import os, tempfile
from reportlab.pdfgen import canvas
import openpyxl
import extrator, excel_io

d = tempfile.mkdtemp()
pasta = os.path.join(d, "AF3242", "COMERCIAL", "Proposta Comercial Valida")
os.makedirs(pasta)
c = canvas.Canvas(os.path.join(pasta, "p.pdf"))
y = 800
for ln in ["Proposta AF3242", "01 Compressor", "Serial Number: SN123", "Model: XYZ-100",
           "Part Number: 5544-33", "Unit Price: R$ 1.250,00", "Total Price: R$ 2.500,00",
           "Lead Time: 12 semanas", "02 Outro", "Model: ZZZ", "Serial Number: SN999"]:
    c.drawString(50, y, ln); y -= 20
c.save()
dados, av, _ = extrator.processar_pedido("AF3242-1", d)
print(dados, av)
assert dados["modelo"] == "XYZ-100" and dados["serial"] == "SN123"
assert dados["preco_total"] == "R$ 2.500,00" and dados["prazo"] == "12 semanas"
x = os.path.join(d, "t.xlsx"); wb = openpyxl.Workbook(); wb.active["K2"] = "AF3242-01"; wb.save(x)
wb = excel_io.abrir(x); ws = wb.active
for l, v in excel_io.listar_pedidos(ws, 2): excel_io.gravar_linha(ws, l, dados)
wb.save(excel_io.caminho_saida(x)); print(openpyxl.load_workbook(excel_io.caminho_saida(x)).active["L2"].value)
