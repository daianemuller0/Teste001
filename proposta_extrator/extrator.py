"""Lógica de extração: localiza o PDF da proposta e lê os dados do item.

O PDF só é aberto em modo leitura; nada é gravado ou apagado na pasta do pedido.
"""
import json
import os
import re
import sys

import pdfplumber

CAMPOS = ["modelo", "serial", "part_number", "preco_unitario", "preco_total", "prazo"]

# Rótulos (regex) procurados no texto do PDF. Podem ser sobrescritos em padroes.json.
PADROES_PADRAO = {
    "modelo": r"(?:Model(?:o)?|Equipment\s*Model|Modelo\s*do\s*Equipamento)",
    "serial": r"(?:Serial\s*(?:Number|No\.?|N[ºo°.]?)?|S/N|N[ºo°.]?\s*de\s*S[ée]rie|N[úu]mero\s*de\s*S[ée]rie)",
    "part_number": r"(?:Part\s*(?:Number|No\.?|N[ºo°.]?)|P/N|PN|C[óo]d(?:igo)?\.?\s*(?:do\s*)?(?:Produto|Item)?)",
    "preco_unitario": r"(?:Unit(?:ary)?\s*Price|Pre[çc]o\s*Unit[áa]rio|Valor\s*Unit[áa]rio|Unit\.?\s*Price)",
    "preco_total": r"(?:Total\s*Price|Pre[çc]o\s*Total|Valor\s*Total|Total\s*Amount)",
    "prazo": r"(?:Lead\s*Time|Prazo(?:\s*de\s*Entrega)?|Delivery(?:\s*Time)?)",
}
CAMPOS_VALOR = {"preco_unitario", "preco_total"}
REGEX_VALOR = re.compile(r"(?:R\$|US\$|\$|€|EUR|USD|BRL)?\s*\d[\d.,]*")


class ErroProposta(Exception):
    pass


def pasta_base_exe():
    return os.path.dirname(sys.executable if getattr(sys, "frozen", False) else os.path.abspath(__file__))


def carregar_padroes():
    padroes = dict(PADROES_PADRAO)
    caminho = os.path.join(pasta_base_exe(), "padroes.json")
    if os.path.exists(caminho):
        with open(caminho, encoding="utf-8") as f:
            padroes.update(json.load(f))
    return padroes


def separar_pedido(valor):
    """'AF3242-01' -> ('AF3242', 1)."""
    texto = str(valor).strip().upper()
    m = re.match(r"^(.+?)\s*-\s*0*(\d+)$", texto)
    if not m:
        raise ErroProposta(f"Pedido em formato inesperado: '{valor}'")
    return m.group(1).strip(), int(m.group(2))


def achar_pdf(base, af, sub="COMERCIAL\\Proposta Comercial Valida"):
    pasta = os.path.join(base, af, *re.split(r"[\\/]", sub))
    if not os.path.isdir(pasta):
        raise ErroProposta(f"Pasta não encontrada: {pasta}")
    pdfs = [os.path.join(pasta, n) for n in sorted(os.listdir(pasta)) if n.lower().endswith(".pdf")]
    if len(pdfs) == 0:
        raise ErroProposta(f"Nenhum PDF em {pasta}")
    if len(pdfs) > 1:
        raise ErroProposta(f"{len(pdfs)} PDFs em {pasta} (esperado 1)")
    return pdfs[0]


def ler_texto(pdf):
    with pdfplumber.open(pdf) as doc:  # somente leitura
        return "\n".join((p.extract_text() or "") for p in doc.pages)


_INICIO_ITEM = re.compile(r"^\s*(?:item\s*)?0*(\d{1,3})(?=\s*[.):\-]|\s+[A-Za-zÀ-ÿ]|\s*$)", re.I)


def bloco_do_item(texto, item):
    """Recorta o trecho do texto referente ao item; sem marcações, devolve tudo."""
    linhas = texto.splitlines()
    marcas = []
    for i, ln in enumerate(linhas):
        m = _INICIO_ITEM.match(ln)
        if m:
            marcas.append((i, int(m.group(1))))
    alvo = [i for i, n in marcas if n == item]
    if not alvo:
        return texto, False
    ini = alvo[0]
    fim = next((i for i, n in marcas if i > ini and n != item), len(linhas))
    return "\n".join(linhas[ini:fim]), True


def _valor_apos_rotulo(linhas, rotulo, campo):
    rx = re.compile(rotulo + r"\s*[:\-–]?\s*(.*)", re.I)
    for i, ln in enumerate(linhas):
        m = rx.search(ln)
        if not m:
            continue
        resto = m.group(1).strip()
        if not resto and i + 1 < len(linhas):
            resto = linhas[i + 1].strip()
        if not resto:
            continue
        if campo in CAMPOS_VALOR:
            v = REGEX_VALOR.search(resto)
            if v:
                return v.group(0).strip()
            continue
        return resto
    return ""


def extrair_campos(texto, item, padroes=None):
    padroes = padroes or carregar_padroes()
    bloco, achou = bloco_do_item(texto, item)
    linhas = [l for l in bloco.splitlines() if l.strip()]
    dados = {c: _valor_apos_rotulo(linhas, padroes[c], c) for c in CAMPOS}
    return dados, achou


def processar_pedido(valor_k, base):
    af, item = separar_pedido(valor_k)
    pdf = achar_pdf(base, af)
    dados, achou = extrair_campos(ler_texto(pdf), item)
    avisos = []
    if not achou:
        avisos.append(f"item {item} não identificado no PDF, usado texto todo")
    faltando = [c for c, v in dados.items() if not v]
    if faltando:
        avisos.append("não encontrado: " + ", ".join(faltando))
    return dados, avisos, pdf
