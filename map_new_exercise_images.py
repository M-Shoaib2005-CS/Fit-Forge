#!/usr/bin/env python3
"""
FitForge — map new exercise GIFs onto their real database IDs.

WHY THIS SCRIPT EXISTS:
FitForge_Migration_ExerciseLibraryExpansion.sql adds 71 new exercises, but
exercise_id is auto-increment, so the final IDs aren't known until AFTER
that migration actually runs against your database. The app expects each
exercise's media at wwwroot/images/exercises/{exercise_id}.gif — this script
looks up the real ID MySQL assigned to each new exercise (by name) and
copies the matching GIF from your local clone of the source dataset
(https://github.com/hasaneyldrm/exercises-dataset) into that path.

USAGE:
    1. Run FitForge_Migration_ExerciseLibraryExpansion.sql first.
    2. Clone the dataset repo somewhere if you haven't already:
         git clone https://github.com/hasaneyldrm/exercises-dataset.git
    3. Install the one dependency: pip install mysql-connector-python
    4. Run this script from the FitForge project root:
         python map_new_exercise_images.py --dataset-path /path/to/exercises-dataset --host localhost --user root --password YOURPW --database defaultdb

The mapping of exercise name -> original GIF path lives in
exercise_name_to_gif_map.json (same folder as this script) — generated
once, alongside the migration, from the exact 71 exercises that were added.
"""
import argparse
import json
import os
import shutil
import sys

def main():
    ap = argparse.ArgumentParser(description="Copy GIFs for the newly-added exercises onto their real DB IDs.")
    ap.add_argument("--dataset-path", required=True, help="Path to your local clone of hasaneyldrm/exercises-dataset")
    ap.add_argument("--host", default="localhost")
    ap.add_argument("--port", type=int, default=3306)
    ap.add_argument("--user", required=True)
    ap.add_argument("--password", required=True)
    ap.add_argument("--database", required=True, help="Real schema name — defaultdb, not fitforgedb")
    ap.add_argument("--images-dir", default="wwwroot/images/exercises", help="Where FitForge expects {id}.gif files")
    args = ap.parse_args()

    map_path = os.path.join(os.path.dirname(os.path.abspath(__file__)), "exercise_name_to_gif_map.json")
    with open(map_path, encoding="utf-8") as f:
        name_to_gif = json.load(f)

    try:
        import mysql.connector
    except ImportError:
        print("Missing dependency. Run: pip install mysql-connector-python")
        sys.exit(1)

    conn = mysql.connector.connect(host=args.host, port=args.port, user=args.user,
                                    password=args.password, database=args.database)
    cur = conn.cursor()

    os.makedirs(args.images_dir, exist_ok=True)

    copied, missing_db, missing_file = 0, [], []
    for name, gif_rel_path in name_to_gif.items():
        cur.execute("SELECT exercise_id FROM exercises WHERE name = %s", (name,))
        row = cur.fetchone()
        if not row:
            missing_db.append(name)
            continue
        exercise_id = row[0]

        src = os.path.join(args.dataset_path, gif_rel_path)
        if not os.path.isfile(src):
            missing_file.append((name, src))
            continue

        dst = os.path.join(args.images_dir, f"{exercise_id}.gif")
        shutil.copyfile(src, dst)
        copied += 1
        print(f"  {exercise_id:>4}.gif  <-  {name}")

    cur.close()
    conn.close()

    print(f"\nDone. Copied {copied} GIFs.")
    if missing_db:
        print(f"\n{len(missing_db)} names not found in the exercises table (migration not run yet, or name mismatch):")
        for n in missing_db: print(f"  - {n}")
    if missing_file:
        print(f"\n{len(missing_file)} GIF source files not found in --dataset-path:")
        for n, p in missing_file: print(f"  - {n}: {p}")

if __name__ == "__main__":
    main()
