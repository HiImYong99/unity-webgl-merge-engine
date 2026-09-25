#!/bin/bash
# CrazyGames preview videos from recorded gameplay frames + the covers.
#   video-landscape.mp4  1920x1080  cover-landscape held 1 s → gameplay: the 1:2
#                                   game as a 540x1080 column on the sunburst backdrop
#   video-portrait.mp4   1080x1620  cover-portrait-800x1200 held 1 s → gameplay:
#                                   the game at 810x1620 on the portrait backdrop
# Backdrops = build_covers.py videobg. H.264 yuv420p (limited-range BT.709),
# +faststart, no audio track, 30 fps.
#
# Usage: store/portals/make_videos.sh [framesDir]   (frames from record_gameplay.cjs)
# The mp4s are generated output (gitignored).
set -euo pipefail
HERE="$(cd "$(dirname "$0")" && pwd)"
FR="${1:-${TMPDIR:-/tmp}/ap-frames}"
FF="${FFMPEG:-/opt/homebrew/bin/ffmpeg}"
ENC=(-c:v libx264 -preset slow -crf 18 -profile:v high -pix_fmt yuv420p -r 30 -movflags +faststart -an
     -colorspace bt709 -color_primaries bt709 -color_trc bt709 -color_range tv)
FADE="xfade=transition=fade:duration=0.3:offset=1.0"   # cover holds 1.0 s, then 0.3 s dissolve
# Compose in RGB, then one conversion to limited-range BT.709 — the JPEG frames
# are full-range and would otherwise leak through as yuvj420p.
OUTFMT="scale=out_color_matrix=bt709:out_range=tv,format=yuv420p"

video() {  # cover, backdrop, W, H, column width, column x, out
  "$FF" -y -loglevel error \
    -loop 1 -framerate 30 -t 1.3 -i "$HERE/$1" \
    -loop 1 -framerate 30 -i "$HERE/$2" \
    -framerate 30 -i "$FR/%05d.jpg" \
    -filter_complex "\
[0:v]fps=30,scale=$3:$4:flags=lanczos,format=gbrp,setsar=1[c];\
[1:v]fps=30,format=gbrp[bg];\
[2:v]fps=30,format=gbrp,scale=$5:$4:flags=lanczos[fg];\
[bg][fg]overlay=$6:0:format=gbrp:shortest=1,setsar=1[g];\
[c][g]$FADE,$OUTFMT[v]" \
    -map "[v]" "${ENC[@]}" "$HERE/$7"
}

video cover-landscape-1920x1080.png video-bg-landscape-1920x1080.png 1920 1080 540 690 video-landscape.mp4
video cover-portrait-800x1200.png video-bg-portrait-1080x1620.png 1080 1620 810 135 video-portrait.mp4

for f in video-landscape video-portrait; do
  printf '%s: ' "$f"
  "${FF%ffmpeg}ffprobe" -v error -show_entries stream=codec_type,codec_name,width,height,pix_fmt,color_range,color_space,r_frame_rate \
    -show_entries format=duration,size -of compact=p=0:nk=1 "$HERE/$f.mp4" | tr '\n' ' '
  echo
done
