<script setup lang="ts">
import { computed } from 'vue'
import type { ComparedFacts, ComparedLine, InvoiceCheck } from '../api'

const props = defineProps<{
  checks: InvoiceCheck[]
  originalId: string
  pdf?: string
}>()

const fields = {
  issue_date: 'Issue date',
  due_date: 'Due date',
  currency: 'Currency',
  purchase_order_reference: 'PO reference',
  iban: 'IBAN',
  declared_subtotal: 'Declared subtotal',
  declared_tax_total: 'Declared tax',
  declared_total: 'Declared total',
} as const

const comparison = computed(() => {
  const check = props.checks.find((check) => check.code === 'DOCUMENT_CONFLICT')
  const original = check?.expected?.normalized_facts?.value as
    ComparedFacts | undefined
  const current = check?.actual?.normalized_facts?.value as
    ComparedFacts | undefined
  if (!original || !current) return
  const keys = Object.keys(fields) as (keyof typeof fields)[]
  return {
    differences: keys
      .filter((key) => original[key] !== current[key])
      .map((key) => ({
        name: fields[key],
        original: original[key],
        current: current[key],
      })),
    lines: [original.lines, current.lines],
  }
})

function lineText(line: ComparedLine) {
  return `${line.sku ?? 'No SKU'} · ${line.quantity} ${line.unit} · unit price ${line.unit_net_price} · tax rate ${line.tax_rate} · net ${line.declared_net_amount} · tax ${line.declared_tax_amount ?? '—'}`
}
</script>

<template>
  <section
    v-if="comparison"
    class="card"
    aria-labelledby="conflict-differences"
  >
    <div class="section-heading">
      <h2 id="conflict-differences">Differences from the original</h2>
      <a v-if="pdf" :href="pdf" target="_blank" rel="noopener"
        >{{ originalId }} PDF ↗</a
      >
      <span v-else class="mono">{{ originalId }}</span>
    </div>
    <div class="table-scroll">
      <table class="fact-table">
        <thead>
          <tr>
            <th scope="col">Fact</th>
            <th scope="col">Original</th>
            <th scope="col">This document</th>
          </tr>
        </thead>
        <tbody>
          <tr
            v-for="{ name, original, current } in comparison.differences"
            :key="name"
          >
            <th scope="row">{{ name }}</th>
            <td class="mono">{{ original ?? '—' }}</td>
            <td class="mono">{{ current ?? '—' }}</td>
          </tr>
          <tr v-if="comparison.lines.some((lines) => lines.length)">
            <th scope="row">Changed lines</th>
            <td v-for="(lines, index) in comparison.lines" :key="index">
              <ul v-if="lines.length" class="fact-values mono">
                <li v-for="(line, lineIndex) in lines" :key="lineIndex">
                  {{ lineText(line) }}
                </li>
              </ul>
              <span v-else>—</span>
            </td>
          </tr>
        </tbody>
      </table>
    </div>
  </section>
</template>
