# Copyright (c) 2026 jojogaming93
# SPDX-License-Identifier: AGPL-3.0-only

"""Generate the dependency-free offline field guide from local Markdown and JSON."""
from pathlib import Path
import html
import json
import re

ROOT = Path(__file__).resolve().parent
CHAPTERS = [('README','Agent handoff'), ('gameplay','Game observations'), ('hud','HUD & profiles'), ('controls','Playing & calibration'), ('technical','Control protocol'), ('architecture','Phone backends'), ('iphone','Experimental iPhone'), ('lessons-learned','Lessons learned'), ('verification','Evidence & tests'), ('maintenance','Maintenance'), ('distribution','Install & release')]

def inline(text):
    tokens = []
    def hold(value):
        tokens.append(value)
        return f'\x00{len(tokens)-1}\x00'
    text = re.sub(r'`([^`]+)`', lambda m: hold('<code>'+html.escape(m[1])+'</code>'), text)
    def link(match):
        label, url = match[1], match[2]
        if url.endswith('.md'):
            url = '#'+Path(url).stem
        if url == 'index.html':
            url = '#atlas'
        return hold(f'<a href="{html.escape(url, quote=True)}">{html.escape(label)}</a>')
    text = re.sub(r'!\[([^]]*)\]\(([^)]+)\)', lambda m: hold(f'<img class="chapter-image" loading="lazy" src="{html.escape(m[2],quote=True)}" alt="{html.escape(m[1],quote=True)}">'), text)
    text = re.sub(r'\[([^]]+)\]\(([^)]+)\)', link, text)
    text = html.escape(text)
    text = re.sub(r'\*\*(.+?)\*\*', r'<strong>\1</strong>', text)
    return re.sub(r'\x00(\d+)\x00', lambda m: tokens[int(m[1])], text)

def markdown(source):
    lines = source.splitlines()
    result=[]; i=0
    while i < len(lines):
        line=lines[i]
        if not line.strip(): i+=1; continue
        if line.startswith('```'):
            language=line[3:]; code=[]; i+=1
            while i<len(lines) and not lines[i].startswith('```'): code.append(lines[i]); i+=1
            result.append(f'<pre><code data-language="{html.escape(language)}">'+html.escape('\n'.join(code))+'</code></pre>'); i+=1; continue
        heading=re.match(r'^(#{1,6}) (.+)',line)
        if heading:
            level=len(heading[1]); result.append(f'<h{level}>{inline(heading[2])}</h{level}>'); i+=1; continue
        if line.startswith('|'):
            rows=[]
            while i<len(lines) and lines[i].startswith('|'):
                cells=[c.strip() for c in lines[i].strip().strip('|').split('|')]
                if not all(re.fullmatch(r':?-+:?',c) for c in cells):rows.append(cells)
                i+=1
            result.append('<div class="table-scroll"><table><thead><tr>'+''.join('<th>'+inline(c)+'</th>' for c in rows[0])+'</tr></thead><tbody>'+''.join('<tr>'+''.join('<td>'+inline(c)+'</td>' for c in row)+'</tr>' for row in rows[1:])+'</tbody></table></div>'); continue
        if re.match(r'^(- |\d+\. )',line):
            ordered=bool(re.match(r'^\d+\. ',line)); tag='ol' if ordered else 'ul'; items=[]
            pattern=r'^\d+\. ' if ordered else r'^- '
            while i<len(lines) and re.match(pattern,lines[i]):
                items.append('<li>'+inline(re.sub(pattern,'',lines[i]))+'</li>'); i+=1
            result.append(f'<{tag}>'+''.join(items)+f'</{tag}>'); continue
        paragraph=[line]; i+=1
        while i<len(lines) and lines[i].strip() and not re.match(r'^(#|\||```|- |\d+\. )',lines[i]): paragraph.append(lines[i]); i+=1
        result.append('<p>'+inline(' '.join(paragraph))+'</p>')
    return '\n'.join(result)

def main():
    chapter_html=[]; nav=[]; search=[]
    for slug,title in CHAPTERS:
        source=(ROOT/f'{slug}.md').read_text(encoding='utf-8')
        chapter_html.append(f'<article id="chapter-{slug}" class="chapter" hidden>{markdown(source)}<a class="source-link" href="{slug}.md">Read Markdown source ↗</a></article>')
        nav.append(f'<a href="#{slug}" data-route="{slug}">{html.escape(title)}</a>')
        search.append(dict(slug=slug,title=title,text=source))
    data=json.loads((ROOT/'data/hud.json').read_text(encoding='utf-8'))
    payload=json.dumps(dict(hud=data,chapters=search),ensure_ascii=False).replace('<','\\u003c')
    template=(ROOT/'page.template.html').read_text(encoding='utf-8')
    output=template.replace('<!-- CHAPTER_NAV -->','\n'.join(nav)).replace('<!-- CHAPTERS -->','\n'.join(chapter_html)).replace('/* EMBEDDED_DATA */',payload)
    (ROOT/'index.html').write_text(output,encoding='utf-8')
    print(f'Built docs/index.html: {len(CHAPTERS)} chapters, {len(data["entries"])} HUD targets.')

if __name__=='__main__':main()
