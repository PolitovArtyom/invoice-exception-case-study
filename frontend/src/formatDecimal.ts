export function formatDecimal(value: string): string {
  return value.replace(/^-?\d+/, (whole) =>
    whole.replace(/\B(?=(?:\d{3})+$)/g, ','),
  )
}
