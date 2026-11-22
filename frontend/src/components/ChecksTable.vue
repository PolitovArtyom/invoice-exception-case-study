<script setup lang="ts">
import { computed, ref } from 'vue'
import Button from 'primevue/button'
import type { InvoiceCheck, InvoiceFacts } from '../api'
import { actualOperands, checkTitle } from '../checkPresentation'
import CheckOperands from './CheckOperands.vue'
import CheckVerdict from './CheckVerdict.vue'

const props = defineProps<{ checks: InvoiceCheck[]; facts: InvoiceFacts }>()
const showPassed = ref(false)
const passed = computed(
  () => props.checks.filter((check) => check.status === 'pass').length,
)
const visibleChecks = computed(() =>
  showPassed.value
    ? props.checks
    : props.checks.filter((check) => check.status !== 'pass'),
)
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
            <th scope="col">Comparison</th>
          </tr>
        </thead>
        <tbody>
          <tr
            v-for="check in visibleChecks"
            :key="`${check.code}/${check.object_id}`"
          >
            <td>
              <strong>{{ checkTitle(check.code) }}</strong><small
                class="mono"
                >{{ check.code }}</small
              >
            </td>
            <td class="mono">{{ check.object_id }}</td>
            <td><CheckVerdict :check="check" /></td>
            <td>
              <details v-if="check.actual && check.expected">
                <summary>View operands</summary>
                <p class="comparison-label">Actual</p>
                <CheckOperands :operands="actualOperands(facts, check)" />
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
