export function formatMoney(amount: string): string {
  return amount.replace(/\B(?=(?:\d{3})+\.)/g, ',')
}
