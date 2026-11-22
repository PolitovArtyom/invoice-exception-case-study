<script setup lang="ts">
import { computed, ref } from 'vue'
import Button from 'primevue/button'
import type { InvoiceCheck } from '../api'
import CheckOperands from './CheckOperands.vue'

const props = defineProps<{ checks: InvoiceCheck[] }>()
const showPassed = ref(false)
const passed = computed(
  () => props.checks.filter((check) => check.status === 'pass').length,
)
const visibleChecks = computed(() =>
  showPassed.value
    ? props.checks
    : props.checks.filter((check) => check.status !== 'pass'),
)

const titles: Record<string, string> = {
  DOCUMENT_CONFLICT: 'Document conflict',
  SUPPLIER_IDENTITY: 'Supplier identity',
  BUYER_IDENTITY: 'Buyer identity',
  ORDER_MATCH: 'Purchase order match',
  CURRENCY_MATCH: 'Currency match',
  LINE_MATCH: 'Invoice line match',
  QTY_RECEIVED: 'Received quantity',
  QTY_ORDER_REMAINING: 'Remaining order quantity',
  UNIT_PRICE: 'Unit price',
  LINE_ARITHMETIC: 'Line arithmetic',
  TOTALS_ARITHMETIC: 'Totals arithmetic',
  BANK_ACCOUNT: 'Bank account',
}
</script>

<template>
  <section class="card" aria-labelledby="checks-title">
    <div class="section-heading">
      <div>
        <h2 id="checks-title">All checks</h2>
        <p class="muted checks-count">
          {{ passed }} of {{ checks.length }} passed
        </p>
      </div>
      <Button
        v-if="passed > 0"
        outlined
        severity="secondary"
        size="small"
        :label="showPassed ? 'Hide passed checks' : 'Show passed checks'"
        aria-controls="invoice-checks"
        :aria-pressed="showPassed"
        @click="showPassed = !showPassed"
      />
    </div>
    <div v-if="visibleChecks.length" id="invoice-checks" class="table-scroll">
      <table>
        <thead>
          <tr>
            <th scope="col">Check</th>
            <th scope="col">Object</th>
            <th scope="col">Verdict</th>
            <th scope="col">Waits for</th>
            <th scope="col">Comparison</th>
          </tr>
        </thead>
        <tbody>
          <tr
            v-for="check in visibleChecks"
            :key="`${check.code}/${check.object_id}`"
          >
            <td>
              <strong>{{ titles[check.code] ?? check.code }}</strong><small
                class="mono"
                >{{ check.code }}</small
              >
            </td>
            <td class="mono">{{ check.object_id }}</td>
            <td>
              <span v-if="check.status === 'pass'" class="pass">✓ Pass</span>
              <span v-else>{{ check.status.replaceAll('_', ' ') }}</span>
              <small v-if="check.severity">{{
                check.severity.replaceAll('_', ' ')
              }}</small>
            </td>
            <td class="mono">{{ check.depends_on ?? '—' }}</td>
            <td>
              <details>
                <summary>View operands</summary>
                <p class="comparison-label">Actual</p>
                <CheckOperands :operands="check.actual" />
                <p class="comparison-label">Expected</p>
                <CheckOperands :operands="check.expected" />
              </details>
            </td>
          </tr>
        </tbody>
      </table>
    </div>
    <p v-else id="invoice-checks" class="collapsed-checks">
      All checks passed.
    </p>
  </section>
</template>
