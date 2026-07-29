#!/usr/bin/env bash
# Board helper for the "Naja Echo Planning" GitHub Project (org project #3).
# Structure is carried by native ISSUE TYPES (Theme/Epic/Feature/Task/Bug) and
# SUB-ISSUE links; the only custom field is Readiness. Resolves project/field/
# option IDs dynamically so it never goes stale.
#
# Usage:
#   board.sh show                              # issue types + Readiness options
#   board.sh themes                            # existing Theme/Epic issues (for parenting)
#   board.sh add <Type> "<title>" [--readiness "<R>"] [--parent <n>]  # create + place + set, one shot
#   board.sh new-theme "<Name>"                # create a Theme issue + add to board
#   board.sh new-epic  "<Name>"                # create an Epic issue  + add to board
#   board.sh place <issue> [--readiness "<R>"] [--parent <n>]   # place an EXISTING issue / set readiness / parent
#   board.sh readiness <issue> "<Readiness>"
#   board.sh type      <issue> "<Type>"        # (re)set issue type
#   board.sh parent    <child> <parent>        # child becomes a sub-issue of parent
#   board.sh blocked-by <issue> <blocker>      # native blocked-by link
#
# <issue> accepts: 123 | #123 | full issue URL
set -euo pipefail

OWNER="naja-echo"
REPO="naja-echo/naja-echo-portal"
PROJECT_NUMBER=3

fields_json() { gh project field-list "$PROJECT_NUMBER" --owner "$OWNER" --format json; }
project_id()  { gh project view "$PROJECT_NUMBER" --owner "$OWNER" --format json --jq '.id'; }
num()         { echo "${1#\#}"; }   # strip a leading # ; URLs pass through gh fine

# option_id <field-name> <option-name>  -> single-select option id (fuzzy, case-insensitive)
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

# place_issue <issue> <readiness-or-empty> <parent-or-empty> : add to board, set fields
place_issue() {
  local issue="$1" readiness="$2" parent="$3" url item
  url=$(issue_url "$issue"); item=$(item_id "$url")
  echo "placing $url"
  [ -n "$readiness" ] && set_field "$item" Readiness "$readiness"
  [ -n "$parent" ] && { gh issue edit "$(num "$issue")" --repo "$REPO" --parent "$(num "$parent")" >/dev/null; echo "  parent = #$(num "$parent")"; }
}

# parse_place_args <args…> : sets globals READINESS / PARENT from --readiness/--parent
parse_place_args() {
  READINESS=""; PARENT=""
  while [ $# -gt 0 ]; do
    case "$1" in
      --readiness) READINESS="$2"; shift 2 ;;
      --parent)    PARENT="$2"; shift 2 ;;
      *) echo "unknown arg $1" >&2; exit 2 ;;
    esac
  done
}

cmd="${1:-}"; shift || true
case "$cmd" in
  show)
    echo "Issue types:"
    gh api "/orgs/$OWNER/issue-types" --jq '.[] | select(.is_enabled) | "  - \(.name): \(.description)"'
    fields_json | python3 -c '
import sys, json
d = json.load(sys.stdin)
for f in d["fields"]:
    if f.get("name") == "Readiness":
        print("Readiness:", ", ".join(o["name"] for o in f.get("options",[])))'
    ;;
  themes)
    gh issue list --repo "$REPO" --state open --limit 200 \
       --json number,title,issueType | python3 -c '
import sys, json
for i in json.load(sys.stdin):
    it = i.get("issueType")
    t = it.get("name") if isinstance(it, dict) else it
    if t in ("Theme","Epic"):
        print("  #%-4d [%s] %s" % (i["number"], t, i["title"]))'
    ;;
  add)   # add <Type> "<title>" [--readiness "<R>"] [--parent <n>]  : create + place + set, one shot
    type="$1"; title="$2"; shift 2
    parse_place_args "$@"
    url=$(gh issue create --repo "$REPO" --type "$type" --title "$title" \
            --body "$type: **$title**. Shaped via the epic-planning skill; see specs/ROADMAP.md.")
    echo "created $url"
    place_issue "$url" "$READINESS" "$PARENT"
    ;;
  new-theme)
    url=$(gh issue create --repo "$REPO" --type Theme --title "$1" \
            --body "Theme: **$1**. Features/epics attach as sub-issues. See specs/ROADMAP.md.")
    item_id "$url" >/dev/null; echo "created + placed: $url"
    ;;
  new-epic)
    url=$(gh issue create --repo "$REPO" --type Epic --title "$1" \
            --body "Epic: **$1**. Bounded initiative; Features attach as sub-issues. Parent under its Theme.")
    item_id "$url" >/dev/null; echo "created + placed: $url"
    ;;
  place)
    issue="$1"; shift
    parse_place_args "$@"
    place_issue "$issue" "$READINESS" "$PARENT"
    ;;
  readiness) url=$(issue_url "$1"); set_field "$(item_id "$url")" Readiness "$2" ;;
  type)      gh issue edit "$(num "$1")" --repo "$REPO" --type "$2" ;;
  parent)    gh issue edit "$(num "$1")" --repo "$REPO" --parent "$(num "$2")" ;;
  blocked-by) gh issue edit "$(num "$1")" --repo "$REPO" --add-blocked-by "$(num "$2")" ;;
  *) grep '^#' "$0" | sed 's/^# \{0,1\}//'; exit 1 ;;
esac
