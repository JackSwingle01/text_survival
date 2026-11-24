"""Builds index.html: page.html with core.js inlined where it says /*CORE*/. Run: python3 build.py"""
from pathlib import Path

here = Path(__file__).parent
page = (here / "page.html").read_text()
core = (here / "core.js").read_text()
(here / "index.html").write_text(page.replace("/*CORE*/", core))
