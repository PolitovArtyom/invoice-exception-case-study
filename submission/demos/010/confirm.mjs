import { join } from 'node:path'
import { firefox } from 'playwright'

const screenshots = process.argv[2] ?? 'submission/demos/010'
const browser = await firefox.launch({
  executablePath: process.env.PLAYWRIGHT_FIREFOX_EXECUTABLE_PATH,
})

try {
  const page = await browser.newPage({ viewport: { width: 1440, height: 1100 } })
  await page.goto('http://127.0.0.1:18130/cases/case-010')
  const proposal = page.getByRole('region', { name: 'Agent proposal' })
  await proposal.getByLabel('Your name').fill('Demo AP Clerk')
  await proposal.getByLabel('I checked the supporting delivery documents').check()
  await proposal.screenshot({
    path: join(screenshots, '01-review-before-confirming.png'),
  })
  await proposal.getByRole('button', { name: 'Confirm this proposal' }).click()
  await proposal.getByText('Confirmed by Demo AP Clerk').waitFor()
  const history = page.getByRole('region', { name: 'History' })
  await history.getByText('Confirmed a proposal').waitFor()
  await history.screenshot({ path: join(screenshots, '02-confirmed-history.png') })
} finally {
  await browser.close()
}
