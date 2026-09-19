find . -type f -name '*.tkr' -exec sh -c '
  for path do
    printf "%s\n" "${path%.*}"
    ../../Scripts/farm_seg_report_help.sh TruthInTheFlip_Farm "$path"  def > "${path%.*}.seg_report.txt"
    ../../Scripts/farm_seg_report_help.sh TruthInTheFlip_Farm "$path"  10B >> "${path%.*}.seg_report.txt"
    done
' sh {} +

