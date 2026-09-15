#!/usr/bin/env python3
"""Parse every C# source with a real grammar and flag build-breaking patterns.

Run from any working directory:
    python3 server-cs/tools/verify_csharp_syntax.py

`verify_server_layout.py` checks the catalog/handler topology and
`verify_server_naming.py` checks the native vocabulary. Neither one parses C#,
so a plain syntax error or a return-type mismatch stays invisible to them until
a machine with the .NET SDK runs a build. This tool closes that gap for the
error classes that have actually broken this repository's build:

* a malformed statement (missing `)`, stray token) -> CS1026 and friends;
* a non-async `Task`/`ValueTask` method that returns a call declared with the
  other of those two types -> CS0029;
* a bare `return;` inside a non-async `Task`/`ValueTask` method -> CS0126;
* a LINQ call on a narrow numeric array (`ushort[]`, `byte[]`, ...) whose
  argument is a bare collection expression of int literals, which makes type
  inference pick `int` and lose the overload -> CS1929.

It is a static check over the syntax tree with only local type reasoning; it
does not resolve symbols and is not a replacement for a real build.

Requires `tree_sitter` and `tree_sitter_c_sharp`. When they are unavailable the
tool skips with exit status 0 so it can sit in a pipeline on machines that only
have the .NET SDK:

    python3 -m venv .venv && .venv/bin/pip install tree_sitter tree_sitter_c_sharp
"""
from __future__ import annotations

import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
SOURCE_ROOT = ROOT / "server-cs" / "src"
GENERATOR_NAME = "PacketHandlerRegistryGenerator.cs"

# Exactly the shape the source generator admits as a handler entry.
HANDLER_SIGNATURE = "(Session session, Packet packet, ServerContext context)"
AWAITABLE_RETURNS = frozenset({"Task", "ValueTask"})
NARROW_ELEMENT_TYPES = frozenset(
    {"ushort", "short", "byte", "sbyte", "uint", "long", "ulong", "float", "double"})
# Sequence-shaped LINQ methods whose TSource is inferred from both the receiver
# and the argument, which is what makes the int-literal collection expression
# win and drop the intended overload.
INFERRING_LINQ_METHODS = frozenset(
    {"SequenceEqual", "Contains", "Except", "Intersect", "Union", "Concat", "Zip"})
# RS1035 bans these inside an analyzer/source-generator assembly.
BANNED_ANALYZER_TYPES = frozenset(
    {"Console", "Process", "ProcessStartInfo", "Environment", "File", "Directory", "Random"})
BANNED_ANALYZER_MEMBERS = frozenset(
    {("CultureInfo", "CurrentCulture"), ("CultureInfo", "CurrentUICulture"),
     ("Path", "GetTempPath"), ("Assembly", "Load")})
BANNED_GENERATOR_CONTEXTS = frozenset(
    {"GeneratorInitializationContext", "GeneratorExecutionContext"})

NARROW_FIELD = re.compile(
    r"\b(?P<type>" + "|".join(sorted(NARROW_ELEMENT_TYPES)) + r")\[\]\s+(?P<name>\w+)\s*[;={,)]")


def build_parser():
    try:
        import tree_sitter_c_sharp
        from tree_sitter import Language, Parser
    except ImportError:
        return None
    return Parser(Language(tree_sitter_c_sharp.language()))


def text(node) -> str:
    return node.text.decode("utf-8", "replace")


def walk(node):
    stack = [node]
    while stack:
        current = stack.pop()
        yield current
        stack.extend(current.children)


def method_parts(node):
    """Return (return_type, name, modifiers, parameter_list, block, arrow).

    The `type` field is not populated by every tree-sitter-c-sharp build, so the
    return type is read positionally: it is the node two slots before the
    parameter list, with the name in between.
    """
    children = list(node.children)
    parameters = next((child for child in children if child.type == "parameter_list"), None)
    if parameters is None:
        return None
    index = children.index(parameters)
    if index < 2 or children[index - 2].type == "modifier":
        return None
    return (
        text(children[index - 2]),
        text(children[index - 1]),
        [text(child) for child in children if child.type == "modifier"],
        parameters,
        next((child for child in children if child.type == "block"), None),
        next((child for child in children if child.type == "arrow_expression_clause"), None),
    )


def returned_expressions(block, arrow):
    """Every expression the method hands back, ignoring nested lambdas."""
    found = []
    if block is not None:
        stack = [block]
        while stack:
            node = stack.pop()
            if node.type in ("lambda_expression", "local_function_statement"):
                continue
            stack.extend(node.children)
            if node.type == "return_statement" and node.named_children:
                found.append(node.named_children[0])
    if arrow is not None and arrow.named_children:
        found.append(arrow.named_children[0])
    return found


def all_integer_literals(collection) -> bool:
    """True for a non-empty collection expression of bare int literals.

    Grammar builds disagree on the wrapper: the literal may sit directly under
    the element, or under a `collection_element` holding an `expression_element`
    holding the literal. Descend through those wrappers instead of assuming a
    fixed depth. An empty element list must not pass, which would make this
    check silently vacuous.
    """
    wrappers = ("collection_element", "expression_element")
    elements = [child for child in collection.named_children if child.type in wrappers]
    if not elements:
        return False
    for element in elements:
        node = element
        while node.type in wrappers and node.named_children:
            node = node.named_children[0]
        if node.type != "integer_literal":
            return False
    return True


def bare_returns(block):
    found = []
    if block is None:
        return found
    stack = [block]
    while stack:
        node = stack.pop()
        if node.type in ("lambda_expression", "local_function_statement"):
            continue
        stack.extend(node.children)
        if node.type == "return_statement" and not node.named_children:
            found.append(node)
    return found


def main() -> None:
    parser = build_parser()
    if parser is None:
        print("C# syntax check skipped: install tree_sitter and tree_sitter_c_sharp to enable it")
        return

    sources = sorted(SOURCE_ROOT.rglob("*.cs"))
    if not sources:
        raise SystemExit(f"C# syntax verification failed: no sources under {SOURCE_ROOT}")

    trees = {path: parser.parse(path.read_bytes()) for path in sources}
    problems: list[str] = []

    def report(path: Path, line: int, code: str, message: str) -> None:
        problems.append(f"{path.relative_to(ROOT)}:{line}: {code}: {message}")

    # Declared return type of every method name, for the cross-file comparison.
    declared_returns: dict[str, set[str]] = {}
    for tree in trees.values():
        for node in walk(tree.root_node):
            if node.type != "method_declaration":
                continue
            parts = method_parts(node)
            if parts:
                declared_returns.setdefault(parts[1], set()).add(parts[0])

    # Names whose declaration proves a narrow element type.
    narrow_arrays: dict[str, str] = {}
    for path in sources:
        for match in NARROW_FIELD.finditer(path.read_text(encoding="utf-8")):
            narrow_arrays[match["name"]] = match["type"]

    handler_entries = 0
    for path, tree in trees.items():
        if tree.root_node.has_error:
            spots = sorted(
                (node.start_point[0] + 1, text(node)[:60].replace("\n", " "))
                for node in walk(tree.root_node)
                if node.type == "ERROR" or node.is_missing)
            line, snippet = spots[0] if spots else (1, "")
            report(path, line, "CS1026", f"source does not parse; first bad token near {snippet!r}")
            continue

        for node in walk(tree.root_node):
            if node.type == "invocation_expression":
                function = node.child_by_field_name("function")
                arguments = node.child_by_field_name("arguments")
                if function is None or arguments is None:
                    continue
                if function.type != "member_access_expression":
                    continue
                name_node = function.child_by_field_name("name")
                receiver = function.child_by_field_name("expression")
                if name_node is None or receiver is None:
                    continue
                method_name = text(name_node)
                if "<" in method_name or method_name not in INFERRING_LINQ_METHODS:
                    continue
                base = text(receiver).split(".")[-1].split("[")[0].split("(")[0]
                element_type = narrow_arrays.get(base)
                if element_type is None:
                    continue
                for argument in arguments.named_children:
                    if not argument.named_children:
                        continue
                    candidate = argument.named_children[0]
                    if candidate.type != "collection_expression":
                        continue
                    if all_integer_literals(candidate):
                        report(
                            path, node.start_point[0] + 1, "CS1929",
                            f"{method_name} on {element_type}[] '{base}' takes an int-literal "
                            f"collection expression; write {method_name}<{element_type}>(...)")

            if node.type != "method_declaration":
                continue
            parts = method_parts(node)
            if parts is None:
                continue
            return_type, name, modifiers, parameters, block, arrow = parts
            if text(parameters) == HANDLER_SIGNATURE and return_type == "ValueTask":
                handler_entries += 1
            if return_type not in AWAITABLE_RETURNS or "async" in modifiers:
                continue

            for statement in bare_returns(block):
                report(
                    path, statement.start_point[0] + 1, "CS0126",
                    f"bare 'return;' in non-async {return_type} {name}(); "
                    f"return {return_type}.CompletedTask")

            for expression in returned_expressions(block, arrow):
                if expression.type != "invocation_expression":
                    continue
                function = expression.child_by_field_name("function")
                if function is None:
                    continue
                callee = text(function).split(".")[-1].split("<")[0]
                candidates = declared_returns.get(callee)
                if not candidates or return_type in candidates:
                    continue
                if candidates & AWAITABLE_RETURNS:
                    report(
                        path, expression.start_point[0] + 1, "CS0029",
                        f"{return_type} {name}() returns {callee}() declared "
                        f"{'/'.join(sorted(candidates))}; these do not convert implicitly")

    # RS1035: the generator assembly opts in to the extended analyzer rules.
    for path, tree in trees.items():
        if path.name != GENERATOR_NAME:
            continue
        for node in walk(tree.root_node):
            if node.type == "member_access_expression":
                receiver = node.child_by_field_name("expression")
                name_node = node.child_by_field_name("name")
                if receiver is None or name_node is None:
                    continue
                base = text(receiver).split(".")[-1]
                if base in BANNED_ANALYZER_TYPES:
                    report(path, node.start_point[0] + 1, "RS1035",
                           f"'{base}' is banned inside an analyzer assembly")
                if (base, text(name_node)) in BANNED_ANALYZER_MEMBERS:
                    report(path, node.start_point[0] + 1, "RS1035",
                           f"'{base}.{text(name_node)}' is banned inside an analyzer assembly")
            elif node.type == "identifier" and text(node) in BANNED_GENERATOR_CONTEXTS:
                report(path, node.start_point[0] + 1, "RS1035",
                       f"'{text(node)}' is banned; implement IIncrementalGenerator")

    if problems:
        print("C# syntax verification failed:")
        for problem in problems:
            print(f"  {problem}")
        raise SystemExit(1)

    print(
        f"C# syntax OK: parsed {len(sources)} sources with no syntax error; "
        f"{handler_entries} handler-shaped entries; no CS0029/CS0126/CS1929 pattern; "
        "no RS1035 banned API in the generator")


if __name__ == "__main__":
    main()
