export function currentVideoRisks(sample, position) {
  const at = sample?.analyzedAt
  // Không vẽ frame AI quá cũ khi người xem đi trước tiến độ phân tích.
  return Number.isFinite(at) && at <= position && position - at <= 0.5 ? sample.risks || [] : []
}
