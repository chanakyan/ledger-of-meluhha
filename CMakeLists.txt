cmake_minimum_required(VERSION 3.20)
project(ledger-of-meluhha NONE)

# ─── The Ledger of Meluhha ───────────────────────────────────────────────────
#
# Build system for the Indus Valley metrological accounting paper.
# CMake discovers tools, wires dependencies, and provides named targets.
#
# Prerequisites:
#   macOS (brew):  brew install dotnet texlive sqlite openjdk@17
#   Linux (apt):   sudo apt install dotnet-sdk-8.0 sqlite3 texlive-full default-jdk
#   Windows:       not supported — use WSL 2 (Ubuntu), see platform gate below
#
# Configure:
#   cmake -S . -B build
#
# Targets:
#   cmake --build build                     # builds everything (all)
#   cmake --build build --target codebook   # seed codebook DB
#   cmake --build build --target corpus     # ingest corpus DB
#   cmake --build build --target lssc       # LSSC analysis
#   cmake --build build --target decode     # run IVC decoder
#   cmake --build build --target tn-decode  # run TN cross-corpus decoder
#   cmake --build build --target verify     # Alloy schema verification
#   cmake --build build --target paper      # compile the paper (lualatex)
#   cmake --build build --target readme     # compile the README
#   cmake --build build --target clean-db   # remove all generated DBs
#
# The indus-valley data directory must be passed at configure time:
#   cmake -S . -B build -DINDUS_VALLEY_DIR=../indus-valley
#
# ─────────────────────────────────────────────────────────────────────────────

# ─── Platform gate ───────────────────────────────────────────────────────────

if(WIN32)
    message(FATAL_ERROR
        "Windows is not supported.\n"
        "\n"
        "  This project targets macOS and Linux only.\n"
        "  The F# scripts, lualatex pipeline, and Alloy verification\n"
        "  are tested on macOS (brew) and Linux (apt).\n"
        "\n"
        "  If you are on Windows, use WSL 2 (Ubuntu):\n"
        "\n"
        "    1. Install WSL:  wsl --install\n"
        "    2. Open Ubuntu:  wsl\n"
        "    3. Install deps:\n"
        "         sudo apt update\n"
        "         sudo apt install -y dotnet-sdk-8.0 sqlite3 texlive-full default-jdk\n"
        "    4. Clone this artifact into the WSL filesystem (~/)\n"
        "    5. Configure:  cmake -S . -B build -DINDUS_VALLEY_DIR=../indus-valley\n"
        "    6. Build:       cmake --build build\n"
        "\n"
        "  Do NOT build from /mnt/c/ — WSL filesystem performance\n"
        "  on Windows drives is 10-100x slower. Work in ~/.\n"
        "\n"
        "  The SQLite databases and PDFs are portable.\n"
        "  Build on WSL, copy the .db and .pdf files anywhere.")
endif()

if(CYGWIN OR MSYS OR MINGW)
    message(FATAL_ERROR
        "Cygwin / MSYS / MinGW are not supported.\n"
        "  Use WSL 2 (Ubuntu) instead. See the WIN32 error above for instructions.")
endif()

# ─── Options ─────────────────────────────────────────────────────────────────

set(INDUS_VALLEY_DIR "" CACHE PATH
    "Path to the indus-valley data directory (contains Preprocessing/, Statistical_Analysis/)")

set(CORPUS_CSV "" CACHE FILEPATH
    "Path to logo_syllabic_tamil_sentences.csv (auto-detected from INDUS_VALLEY_DIR if set)")

# ─── Find tools (fail loudly) ────────────────────────────────────────────────

find_program(DOTNET dotnet)
find_program(SQLITE3 sqlite3)
find_program(LUALATEX lualatex)
find_program(JAVA java HINTS /opt/homebrew/opt/openjdk@17/bin)

# --- dotnet (required — every F# script needs it) ---
if(NOT DOTNET)
    message(FATAL_ERROR
        "MISSING: dotnet\n"
        "  All F# scripts require the .NET SDK.\n"
        "  Fix: brew install dotnet\n"
        "  Verify: dotnet fsi --version")
endif()

execute_process(
    COMMAND ${DOTNET} fsi --help
    OUTPUT_QUIET ERROR_QUIET
    RESULT_VARIABLE DOTNET_FSI_RESULT)
if(NOT DOTNET_FSI_RESULT EQUAL 0)
    message(FATAL_ERROR
        "BROKEN: dotnet found at ${DOTNET} but 'dotnet fsi' fails.\n"
        "  Fix: brew reinstall dotnet\n"
        "  The .NET SDK must include F# Interactive (fsi).")
endif()

# --- sqlite3 (required — DB inserts and queries) ---
if(NOT SQLITE3)
    message(FATAL_ERROR
        "MISSING: sqlite3\n"
        "  The corpus target uses sqlite3 to insert morphological parallels.\n"
        "  Fix: brew install sqlite (or it ships with macOS — check PATH)\n"
        "  Verify: sqlite3 --version")
endif()

# --- lualatex (required — the paper and README are LaTeX) ---
if(NOT LUALATEX)
    message(FATAL_ERROR
        "MISSING: lualatex\n"
        "  The paper (ledger_of_meluhha.tex) requires LuaLaTeX.\n"
        "  Fix: brew install --cask mactex   (full TeX Live)\n"
        "    or: brew install texlive         (minimal)\n"
        "  Verify: lualatex --version")
endif()

# --- java (required — Alloy schema verification) ---
if(NOT JAVA)
    message(FATAL_ERROR
        "MISSING: java\n"
        "  Alloy schema verification requires Java 17+.\n"
        "  Fix: brew install openjdk@17\n"
        "  Verify: java -version")
endif()

# --- alloy.jar (required — schema invariants) ---
find_file(ALLOY_JAR alloy.jar
    PATHS
        ${CMAKE_SOURCE_DIR}/../../../tools/alloy
        $ENV{HOME}/bin
        $ENV{HOME}/tba-research-python/tools/alloy
    NO_DEFAULT_PATH)

if(NOT ALLOY_JAR)
    message(FATAL_ERROR
        "MISSING: alloy.jar\n"
        "  Alloy 6 is required for schema verification.\n"
        "  Searched:\n"
        "    ${CMAKE_SOURCE_DIR}/../../../tools/alloy/\n"
        "    $ENV{HOME}/bin/\n"
        "    $ENV{HOME}/tba-research-python/tools/alloy/\n"
        "  Fix: download from https://github.com/AlloyTools/org.alloytools.alloy\n"
        "       and place alloy.jar in one of the paths above.")
endif()

# --- INDUS_VALLEY_DIR (required) ---
if(NOT INDUS_VALLEY_DIR)
    message(FATAL_ERROR
        "MISSING: -DINDUS_VALLEY_DIR=<path>\n"
        "  The corpus and LSSC targets need the indus-valley data directory.\n"
        "  Fix: cmake -S . -B build -DINDUS_VALLEY_DIR=../indus-valley\n"
        "  The directory must contain Preprocessing/ and Statistical_Analysis/.")
endif()

if(NOT EXISTS "${INDUS_VALLEY_DIR}/Preprocessing")
    message(FATAL_ERROR
        "BAD PATH: INDUS_VALLEY_DIR=${INDUS_VALLEY_DIR}\n"
        "  Directory exists but Preprocessing/ subfolder not found.\n"
        "  Expected: ${INDUS_VALLEY_DIR}/Preprocessing/Converted_Tamil/LogoSyllabic/")
endif()

# --- corpus CSV (auto-detect from INDUS_VALLEY_DIR, then verify) ---
if(NOT CORPUS_CSV)
    set(CORPUS_CSV
        "${INDUS_VALLEY_DIR}/Preprocessing/Converted_Tamil/LogoSyllabic/logo_syllabic_tamil_sentences.csv")
endif()
if(NOT EXISTS "${CORPUS_CSV}")
    message(FATAL_ERROR
        "MISSING: corpus CSV not found at:\n"
        "  ${CORPUS_CSV}\n"
        "  Fix: verify INDUS_VALLEY_DIR points to a complete indus-valley checkout.")
endif()

# --- all good ---
message(STATUS "")
message(STATUS "=== DEPENDENCY CHECK PASSED ===")
message(STATUS "dotnet   : ${DOTNET}")
message(STATUS "sqlite3  : ${SQLITE3}")
message(STATUS "lualatex : ${LUALATEX}")
message(STATUS "java     : ${JAVA}")
message(STATUS "alloy.jar: ${ALLOY_JAR}")
message(STATUS "data dir : ${INDUS_VALLEY_DIR}")
message(STATUS "corpus   : ${CORPUS_CSV}")
message(STATUS "")

# ─── Resolve paths ───────────────────────────────────────────────────────────

set(SRC ${CMAKE_SOURCE_DIR})
set(OUT ${CMAKE_BINARY_DIR})

# ─── Target: codebook ────────────────────────────────────────────────────────

add_custom_command(
    OUTPUT  ${SRC}/indus_codebook.db
    COMMAND ${DOTNET} fsi ${SRC}/indus_seed_codebook.fsx ${SRC}/indus_codebook.db
    DEPENDS ${SRC}/indus_seed_codebook.fsx
    WORKING_DIRECTORY ${SRC}
    COMMENT "Seeding indus_codebook.db")

add_custom_target(codebook DEPENDS ${SRC}/indus_codebook.db)

# ─── Target: corpus ──────────────────────────────────────────────────────────

add_custom_command(
    OUTPUT  ${SRC}/indus_corpus.db
    COMMAND ${DOTNET} fsi ${SRC}/indus_ingest_corpus.fsx
            ${INDUS_VALLEY_DIR} ${SRC}/indus_corpus.db
    COMMAND ${SQLITE3} ${SRC}/indus_corpus.db
            ".read ${SRC}/data/morphological-parallels/insert_parallels.sql"
    DEPENDS
        ${SRC}/indus_ingest_corpus.fsx
        ${SRC}/data/morphological-parallels/insert_parallels.sql
        ${SRC}/data/morphological-parallels/rs2025_plate_parallels.csv
    WORKING_DIRECTORY ${SRC}
    COMMENT "Ingesting corpus into indus_corpus.db")

add_custom_target(corpus DEPENDS ${SRC}/indus_corpus.db)

# ─── Target: lssc ────────────────────────────────────────────────────────────

add_custom_command(
    OUTPUT  ${SRC}/indus_lssc.db
    COMMAND ${DOTNET} fsi ${SRC}/indus_lssc.fsx
            ${CORPUS_CSV} ${SRC}/indus_lssc.db
    DEPENDS ${SRC}/indus_lssc.fsx
    WORKING_DIRECTORY ${SRC}
    COMMENT "Computing LSSC analysis into indus_lssc.db")

add_custom_target(lssc DEPENDS ${SRC}/indus_lssc.db)

# ─── Target: decode ──────────────────────────────────────────────────────────

add_custom_target(decode
    COMMAND ${DOTNET} fsi ${SRC}/indus_decoder.fsx ${SRC}/indus_codebook.db
    DEPENDS codebook
    WORKING_DIRECTORY ${SRC}
    COMMENT "Running Indus cargo-tag decoder")

# ─── Target: tn-decode ───────────────────────────────────────────────────────

add_custom_target(tn-decode
    COMMAND ${DOTNET} fsi ${SRC}/indus_tn_decoder.fsx
            ${SRC}/indus_corpus.db ${SRC}/indus_codebook.db
    DEPENDS codebook corpus
    WORKING_DIRECTORY ${SRC}
    COMMENT "Running Tamil Nadu cross-corpus decoder")

# ─── Target: verify ──────────────────────────────────────────────────────────

add_custom_target(verify
    COMMAND ${JAVA} -jar ${ALLOY_JAR} exec ${SRC}/spec/indus_corpus.als
    WORKING_DIRECTORY ${SRC}
    COMMENT "Alloy schema verification (9 assertions)")

# ─── Target: paper ───────────────────────────────────────────────────────────

add_custom_target(paper
    COMMAND ${LUALATEX} -interaction=nonstopmode -shell-escape ledger_of_meluhha.tex
    COMMAND ${LUALATEX} -interaction=nonstopmode -shell-escape ledger_of_meluhha.tex
    WORKING_DIRECTORY ${SRC}
    COMMENT "Compiling ledger_of_meluhha.pdf (2 passes)")

add_custom_target(readme
    COMMAND ${LUALATEX} -interaction=nonstopmode README.tex
    WORKING_DIRECTORY ${SRC}
    COMMENT "Compiling README.pdf")

# ─── Target: clean-db ────────────────────────────────────────────────────────
# Removes all generated databases so targets rebuild from scratch

add_custom_target(clean-db
    COMMAND ${CMAKE_COMMAND} -E rm -f
        ${SRC}/indus_codebook.db
        ${SRC}/indus_corpus.db
        ${SRC}/indus_lssc.db
    COMMENT "Removing generated databases")

# ─── Target: clean-tex ───────────────────────────────────────────────────────
# Removes LaTeX build artifacts

add_custom_target(clean-tex
    COMMAND ${CMAKE_COMMAND} -E rm -f
        ${SRC}/ledger_of_meluhha.aux
        ${SRC}/ledger_of_meluhha.log
        ${SRC}/ledger_of_meluhha.out
        ${SRC}/ledger_of_meluhha.xmpdata
        ${SRC}/README.aux
        ${SRC}/README.log
        ${SRC}/README.out
        ${SRC}/pdfa.xmpi
    COMMAND ${CMAKE_COMMAND} -E rm -rf ${SRC}/indus_corpus
    COMMENT "Removing LaTeX build artifacts")

# ─── Target: clean-all ───────────────────────────────────────────────────────
# Full clean: DBs + TeX artifacts + build dir

add_custom_target(clean-all
    COMMAND ${CMAKE_COMMAND} -E rm -f
        ${SRC}/indus_codebook.db
        ${SRC}/indus_corpus.db
        ${SRC}/indus_lssc.db
        ${SRC}/ledger_of_meluhha.aux
        ${SRC}/ledger_of_meluhha.log
        ${SRC}/ledger_of_meluhha.out
        ${SRC}/ledger_of_meluhha.xmpdata
        ${SRC}/ledger_of_meluhha.pdf
        ${SRC}/README.aux
        ${SRC}/README.log
        ${SRC}/README.out
        ${SRC}/README.pdf
        ${SRC}/pdfa.xmpi
    COMMAND ${CMAKE_COMMAND} -E rm -rf ${SRC}/indus_corpus ${SRC}/build
    COMMENT "Full clean: databases + TeX artifacts + build dir")

# ─── ALL ─────────────────────────────────────────────────────────────────────
# Default build: codebook + corpus + LSSC (all dependencies verified above)

add_custom_target(all-targets ALL DEPENDS codebook corpus lssc)
