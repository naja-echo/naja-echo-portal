#!/usr/bin/env bash
# Board helper for the "Naja Echo Planning" GitHub Project (org project #3).
# Resolves project/field/option IDs dynamically so it never goes stale.
#
# Usage:
#   board.sh place    <issue> --epic "<Epic>" --readiness "<Readiness>"
#   board.sh readiness <issue> "<Readiness>"
#   board.sh epic      <issue> "<Epic>"
#   board.sh new-epic  "<Name>"           # opens [Epic] <Name> tracking issue
#   board.sh attach    <feature> <epic>   # feature becomes a sub-issue of epic
#   board.sh blocked-by <issue> <blocker> # native blocked-by link
#   board.sh show                         # print epic/readiness option names
#
# <issue> accepts: 123 | #123 | full issue URL
set -euo pipefail

OWNER="Deceptively-Clever"
REPO="Deceptively-Clever/naja-echo-portal"
PROJECT_NUMBER=3

fields_json() { gh project field-list "$PROJECT_NUMBER" --owner "$OWNER" --format json; }
project_id()  { gh project view "$PROJECT_NUMBER" --owner "$OWNER" --format json --jq '.id'; }

# option_id <field-name> <option-name>  -> prints the single-select option id (fuzzy, case-insensitive)
option_id() {
  fields_json | FIELD="$1" OPT="$2" python3 -c '
import sys, json, os
d = json.load(sys.stdin)
fname, oname = os.environ["FIELD"].lower(), os.environ["OPT"].strip().lower()
for f in d["fields"]:
    if f.get("name","").lower() == fname:
        for o in f.get("options", []):
            if o["name"].lower() == oname:
                print(o["id"]); sys.exit(0)
        # fuzzy contains
        for o in f.get("options", []):
            if oname in o["name"].lower():
                print(o["id"]); sys.exit(0)
        sys.stderr.write("No option [" + os.environ["OPT"] + "] in field " + os.environ["FIELD"] + "\n")
        sys.exit(3)
sys.stderr.write("No field " + os.environ["FIELD"] + "\n"); sys.exit(4)'
}

field_id() {
  fields_json | FIELD="$1" python3 -c '
import sys, json, os
d = json.load(sys.stdin); fname = os.environ["FIELD"].lower()
for f in d["fields"]:
    if f.get("name","").lower() == fname:
        print(f["id"]); sys.exit(0)
sys.exit(4)'
}

issue_url() {
  case "$1" in
    http*) echo "$1" ;;
    \#*)   echo "https://github.com/$REPO/issues/${1#\#}" ;;
    *)     echo "https://github.com/$REPO/issues/$1" ;;
  esac
}

# item_id <issue-url> : add to project if needed, return the project item id
item_id() {
  local url="$1" id
  id=$(gh project item-add "$PROJECT_NUMBER" --owner "$OWNER" --url "$url" --format json --jq '.id' 2>/dev/null) || true
  if [ -z "${id:-}" ]; then
    id=$(gh project item-list "$PROJECT_NUMBER" --owner "$OWNER" --limit 200 --format json \
         | URL="$url" python3 -c '
import sys, json, os
d = json.load(sys.stdin); url = os.environ["URL"]
for it in d["items"]:
    c = it.get("content", {})
    if c.get("url") == url: print(it["id"]); break')
  fi
  [ -n "${id:-}" ] || { echo "could not resolve project item for $url" >&2; exit 5; }
  echo "$id"
}

set_field() { # set_field <item-id> <field-name> <option-name>
  local item="$1" fname="$2" oname="$3"
  gh project item-edit --id "$item" --project-id "$(project_id)" \
     --field-id "$(field_id "$fname")" \
     --single-select-option-id "$(option_id "$fname" "$oname")" >/dev/null
  echo "  set $fname = $oname"
}

cmd="${1:-}"; shift || true
case "$cmd" in
  show)
    fields_json | python3 -c '
import sys, json
d = json.load(sys.stdin)
for f in d["fields"]:
    if f.get("name") in ("Readiness","Epic"):
        print(f["name"]+":", ", ".join(o["name"] for o in f.get("options",[])))'
    ;;
  place)
    issue="$1"; shift
    epic=""; readiness=""
    while [ $# -gt 0 ]; do
      case "$1" in
        --epic) epic="$2"; shift 2 ;;
        --readiness) readiness="$2"; shift 2 ;;
        *) echo "unknown arg $1" >&2; exit 2 ;;
      esac
    done
    url=$(issue_url "$issue"); item=$(item_id "$url")
    echo "placing $url"
    [ -n "$epic" ] && set_field "$item" Epic "$epic"
    [ -n "$readiness" ] && set_field "$item" Readiness "$readiness"
    ;;
  readiness) url=$(issue_url "$1"); set_field "$(item_id "$url")" Readiness "$2" ;;
  epic)      url=$(issue_url "$1"); set_field "$(item_id "$url")" Epic "$2" ;;
  attach)    gh issue edit "${1#\#}" --repo "$REPO" --add-sub-issue "${2#\#}" 2>/dev/null \
               || gh issue edit "${1#\#}" --repo "$REPO" --parent "${2#\#}" ;;
  blocked-by) gh issue edit "${1#\#}" --repo "$REPO" --add-blocked-by "${2#\#}" ;;
  new-epic)
    gh issue create --repo "$REPO" --title "[Epic] $1" \
       --body "Tracking issue for the **$1** epic. Feature issues attach here as sub-issues. See specs/ROADMAP.md." ;;
  *) grep '^#' "$0" | sed 's/^# \{0,1\}//'; exit 1 ;;
esac
