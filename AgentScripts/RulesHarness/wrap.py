"""Wraps eval-style Tests/*.cs scripts (top-level statements ending in `return "..."`) as static methods.

Usage: python wrap.py MatchRulesChecks SettlementChecks ...   (names without .cs)
"""
import os
import sys

here = os.path.dirname(os.path.abspath(__file__))
root = os.path.join(here, '..', '..', 'Tests')
out = os.path.join(here, 'Generated')
os.makedirs(out, exist_ok=True)
for stale in os.listdir(out):
    os.remove(os.path.join(out, stale))
names = sys.argv[1:]
for name in names:
    src = open(os.path.join(root, name + '.cs'), encoding='utf-8').read()
    body = '\n'.join(l for l in src.splitlines() if not l.lstrip().startswith('using '))
    usings = '\n'.join(l for l in src.splitlines() if l.lstrip().startswith('using '))
    wrapped = f"""using System;
using System.Linq;
using System.Collections.Generic;
{usings}
public static class Check_{name}
{{
    public static string Run()
    {{
{body}
    }}
}}
"""
    open(os.path.join(out, name + '.cs'), 'w', encoding='utf-8').write(wrapped)
print('wrapped', names)
