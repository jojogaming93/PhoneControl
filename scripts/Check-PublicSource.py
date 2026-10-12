# Copyright (c) 2026 jojogaming93
# SPDX-License-Identifier: AGPL-3.0-only
"""Validate publishable files, generated images and optional public Git history.

PUBLIC_DENY_TERMS may contain a JSON array of private identifiers for local audits.
Only paths/reasons are reported, never matching values. Secret scanning is also
required; this check does not replace Gitleaks or a review of new attachments.
"""
import argparse
import importlib.util
import io
import hashlib
import json
import os
from pathlib import Path
import re
import subprocess
import sys
import zipfile

ROOT = Path(__file__).resolve().parent.parent
ROOT_FILES = {'.gitignore', '.gitattributes', 'AGENTS.md', 'global.json', 'version.props', 'LICENSE',
              'README.md', 'THIRD-PARTY-NOTICES.md', 'VALIDATION.md', 'SOURCE.md',
              'PUBLIC-RELEASE.md', 'Install-Dependencies.ps1', 'Launch-PhoneControl.ps1'}
ROOTS = {'src', 'server', 'scripts', 'installer', 'assets', 'docs', '.github'}
EXTENSIONS = {'.cs', '.csproj', '.xaml', '.manifest', '.json', '.java', '.ps1', '.xml',
              '.iss', '.cpp', '.h', '.rc', '.py', '.md', '.html', '.js', '.css',
              '.svg', '.png', '.ico', '.jar', '.yml', '.yaml'}
PRIVATE_NAMES = re.compile(r'(?i)(^|/)(\.env(?:\..*)?|adbkey.*|settings\.json|credentials\.json|secrets\.json|auth\.json)$|\.(pfx|p12|pem|key|keystore|jks|log|dmp|pdb|exe|dll|zip|7z|bundle)$')
EMAIL = re.compile(r'[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}')
LOCAL_PATH = re.compile(r'(?i)(?:[a-z]:[/\\]Users[/\\][A-Za-z0-9_. -]+[/\\]|/(?:home|Users)/[A-Za-z0-9_.-]+/)')
PRIVATE_KEY = re.compile(r'-----BEGIN (?:[A-Z]+ )?PRIVATE KEY-----')


def git(*args):
    return subprocess.check_output(['git', '-C', str(ROOT), *args])


def source_files():
    paths = git('-c', 'core.quotePath=false', 'ls-files', '--cached', '--others', '--exclude-standard', '-z').decode('utf-8').split('\0')
    return sorted({p for p in paths if p and (ROOT / p).is_file()})


def allowed(path):
    p = Path(path)
    return (path in ROOT_FILES or path == 'server/LICENSE.scrcpy' or
            (p.parts[0] in ROOTS and p.suffix.lower() in EXTENSIONS)) and not PRIVATE_NAMES.search(path)


def findings(data, deny_terms):
    reasons = set()
    for encoding in ('utf-8', 'utf-16-le'):
        text = data.decode(encoding, errors='ignore')
        if any(term and term.casefold() in text.casefold() for term in deny_terms):
            reasons.add('private identifier')
        if any(not address.endswith('@example.invalid') for address in EMAIL.findall(text)):
            reasons.add('email address')
        if LOCAL_PATH.search(text):
            reasons.add('personal local path')
        if PRIVATE_KEY.search(text):
            reasons.add('private key')
    return sorted(reasons)


def inspect(data, label, deny_terms, issues):
    # Read the prebuilt JAR's contents too, rather than scanning compressed bytes.
    if data.startswith(b'PK\x03\x04'):
        with zipfile.ZipFile(io.BytesIO(data)) as archive:
            for member in archive.infolist():
                for reason in findings(member.filename.encode(), deny_terms):
                    issues.append((label + '::member path', reason))
                if not member.is_dir():
                    inspect(archive.read(member), label + '::' + member.filename, deny_terms, issues)
    else:
        for reason in findings(data, deny_terms):
            issues.append((label, reason))


def validate(history=False):
    deny_terms = json.loads(os.environ.get('PUBLIC_DENY_TERMS', '[]'))
    if not isinstance(deny_terms, list) or not all(isinstance(x, str) for x in deny_terms):
        raise ValueError('PUBLIC_DENY_TERMS must be a JSON array of strings.')
    issues = []
    spec = importlib.util.spec_from_file_location('fixtures', ROOT / 'docs/generate_fixtures.py')
    generator = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(generator)
    generated = {'docs/' + p: content for p, content in generator.outputs().items()}
    # Exact reviewed AI artwork is allowed separately from reproducible fixtures.
    # Never extend this exception to arbitrary images or device captures.
    approved_branding = {
        'assets/branding/phonecontrol-logo.png': 'b344071acbc49f36794fd84d1dc23aa41057796f22e135c03b10990ec84088a9',
        'assets/branding/phonecontrol.ico': 'ebbac9956b793ad50c70b800b8944e00d157510e37618f119c6c476c7fdd0ea8',
    }
    files = source_files()
    for path in files:
        if not allowed(path):
            issues.append((path, 'file outside public source allowlist'))
        inspect(path.encode(), 'path: ' + path, deny_terms, issues)
        data = (ROOT / path).read_bytes()
        approved = path in approved_branding and hashlib.sha256(data).hexdigest() == approved_branding[path]
        # Reviewed image bytes are pinned. The logo's generation provenance
        # includes public CA contact metadata, not personal contact information.
        # Binary regex scans cannot distinguish it or inspect visual content.
        if not approved:
            inspect(data, path, deny_terms, issues)
        if Path(path).suffix.lower() in {'.png', '.ico', '.jpg', '.jpeg', '.svg'}:
            if not approved and (path not in generated or data != generated[path]):
                issues.append((path, 'image is not the reproducible public fixture/diagram'))
    for path in generated:
        if path not in files:
            issues.append((path, 'required generated fixture missing'))
    if history:
        inspect(git('log', '--all', '--format=fuller'), 'Git author/committer/messages', deny_terms, issues)
        # Enforce public synthetic attribution and never import private ancestry.
        identities = git('log', '--all', '--format=%an|%ae|%cn|%ce').decode().splitlines()
        expected = 'jojogaming93|noreply@example.invalid|jojogaming93|noreply@example.invalid'
        if any(identity != expected for identity in identities):
            issues.append(('Git history', 'unexpected author/committer identity'))
        for line in git('rev-list', '--objects', '--all').decode('utf-8').splitlines():
            oid, _, path = line.partition(' ')
            if git('cat-file', '-t', oid).strip() == b'blob':
                if not path or not allowed(path):
                    issues.append((path or oid, 'history blob outside public allowlist'))
                blob = git('cat-file', 'blob', oid)
                approved = path in approved_branding and hashlib.sha256(blob).hexdigest() == approved_branding[path]
                if not approved:
                    inspect(blob, path or oid, deny_terms, issues)
        inspect(git('config', '--local', '--list'), 'local Git configuration', deny_terms, issues)
    for label, reason in sorted(set(issues)):
        print(f'FAIL {label}: {reason}')
    if issues:
        return 1
    print(f'Public source check passed: {len(files)} files; generated images verified' + ('; all Git history inspected.' if history else '.'))
    return 0


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--history', action='store_true', help='Validate every public commit, identity and blob.')
    sys.exit(validate(parser.parse_args().history))
