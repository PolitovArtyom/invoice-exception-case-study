export function formatDecimal(value: string): string {
  return value.replace(/^-?\d+/, (whole) =>
    whole.replace(/\B(?=(?:\d{3})+$)/g, ','),
  )
}

export function formatAmount(value: unknown, currency: string | null): string {
  if (typeof value !== 'string') return 'Unknown'
  const formatted = formatDecimal(value)
  return currency ? `${formatted} ${currency}` : formatted
}
