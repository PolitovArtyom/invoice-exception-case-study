import { join } from 'node:path'
import { firefox } from 'playwright'

const baseUrl = process.env.INVOICE_BASE_URL || 'http://127.0.0.1:8080'
const outputDirectory = process.argv[2] ?? 'submission/demos/010'
const browser = await firefox.launch({
  executablePath: process.env.PLAYWRIGHT_FIREFOX_EXECUTABLE_PATH,
})

try {
  const context = await browser.newContext({
    viewport: { width: 1440, height: 1000 },
    recordVideo: { dir: outputDirectory, size: { width: 1280, height: 888 } },
  })
  const page = await context.newPage()
  await page.goto(new URL('/cases/case-010/revisions/2', baseUrl).href)
  await page.getByRole('region', { name: 'Needs attention' }).waitFor()
  await page.evaluate(() => {
    const banner = document.createElement('div')
    banner.id = 'demo-caption'
    Object.assign(banner.style, {
      position: 'fixed',
      inset: '0 0 auto',
      zIndex: '10000',
      padding: '10px 24px',
      background: '#263449',
      color: 'white',
      font: '600 18px/24px system-ui, sans-serif',
      pointerEvents: 'none',
    })
    document.body.append(banner)
  })

  const caption = (text) =>
    page.locator('#demo-caption').evaluate((banner, text) => {
      banner.textContent = text
    }, text)
  const show = (element) =>
    element.evaluate((target) => {
      target.style.scrollMarginTop = '54px'
      target.scrollIntoView({ behavior: 'smooth', block: 'start' })
    })

  await caption('Агент прочитал PDF и зарегистрировал данные через MCP. Приложение запросило заказ.')
  await page.waitForTimeout(4000)
  await page.getByRole('link', { name: 'Open the latest' }).click()
  const proposal = page.getByRole('region', { name: 'Agent proposal' })
  await proposal.getByText('Accepted receipts', { exact: true }).waitFor()
  await caption('Агент предложил ORD-010 и связал L01 с позицией, по которой есть приёмка.')
  await show(proposal)
  await page.waitForTimeout(4200)

  const preview = proposal.getByText('If you confirm this proposal')
  await caption('Предварительная проверка показывает Clear после подтверждения.')
  await show(preview)
  await page.waitForTimeout(2400)

  await caption('Бухгалтер указывает имя и отмечает проверку документов о поставке.')
  const clerkName = proposal.getByLabel('Your name')
  await clerkName.click()
  await clerkName.pressSequentially('Demo AP Clerk', { delay: 85 })
  await page.waitForTimeout(650)
  await proposal.getByLabel('I checked the supporting delivery documents').check()
  await page.waitForTimeout(1300)
  await proposal.screenshot({
    path: join(outputDirectory, '01-review-before-confirming.png'),
  })
  await proposal.getByRole('button', { name: 'Confirm this proposal' }).click()
  await proposal.getByText('Confirmed by Demo AP Clerk').waitFor()
  await caption('Сопоставление подтверждено. Счёт готов к передаче в бухгалтерию.')
  await show(page.locator('.case-header'))
  await page.waitForTimeout(3100)

  const history = page.getByRole('region', { name: 'History' })
  await history.getByText('Confirmed a proposal').waitFor()
  await caption('История показывает оба предложения, подтверждение и результат Clear.')
  await show(history)
  await page.waitForTimeout(4100)
  await history.screenshot({ path: join(outputDirectory, '02-confirmed-history.png') })
  await context.close()
  const video = page.video()
  await video.saveAs(join(outputDirectory, 'demo.webm'))
  await video.delete()
} finally {
  await browser.close()
}
