; Analyzer release tracking — shipped releases (RS2008).
;
; Microsoft.CodeAnalysis.Analyzers only treats release tracking as enabled when
; BOTH AnalyzerReleases.Shipped.md and AnalyzerReleases.Unshipped.md are passed
; as AdditionalFiles, so this file must exist even while it is still empty.
;
; PaperMan.HandlerGenerator is an internal compile-time component of this
; repository and has never been published as a versioned analyzer package, so
; no PMH* rule has shipped yet. The first time it is released under a version
; number, move the entries from AnalyzerReleases.Unshipped.md to a new
; "## Release <version>" section appended at the end of this file, and leave the
; unshipped file with no rule rows.
;
; Only lines beginning with ';' are treated as comments by the release-tracking
; parser. HTML/Markdown comments are parsed as content and fail the build.
