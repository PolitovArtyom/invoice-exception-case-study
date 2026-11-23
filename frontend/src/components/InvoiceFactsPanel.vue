<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import type { CorrectableFact, InvoiceFacts } from '../api'
import InvoiceFactSource from './InvoiceFactSource.vue'

const props = defineProps<{
  facts: InvoiceFacts
  pdf?: string
  caseId: string
  revision: number
  correctableFacts: CorrectableFact[]
}>()
const emit = defineEmits<{ refresh: [message: string] }>()
const editing = ref('')
watch(
  () => props.revision,
  () => {
    editing.value = ''
  },
)
const editable = computed(
  () => new Map(props.correctableFacts.map((field) => [field.path, field])),
)

function refresh(message: string) {
  editing.value = ''
  emit('refresh', message)
}

type FactRow = [path: string, label: string, value: string | null]

const groups = computed<Record<string, FactRow[]>>(() => {
  const { facts } = props
  return {
    Document: [
      ['/document_id', 'Document ID', facts.document_id],
      ['/type', 'Document type', facts.type],
      ['/schema_version', 'Extraction schema version', facts.schema_version],
      ['/number', 'Invoice number', facts.number],
      ['/issue_date', 'Issue date', facts.issue_date],
      ['/due_date', 'Due date', facts.due_date],
      ['/currency', 'Currency', facts.currency],
      [
        '/purchase_order_reference',
        'PO reference',
        facts.purchase_order_reference,
      ],
      ['/payment_terms', 'Payment terms', facts.payment_terms],
    ],
    Parties: [
      ['/supplier/legal_name', 'Supplier name', facts.supplier.legal_name],
      [
        '/supplier/registration_id',
        'Supplier registration ID',
        facts.supplier.registration_id,
      ],
      ['/supplier/country', 'Supplier country', facts.supplier.country],
      ['/buyer/legal_name', 'Buyer name', facts.buyer.legal_name],
      [
        '/buyer/registration_id',
        'Buyer registration ID',
        facts.buyer.registration_id,
      ],
      ['/buyer/country', 'Buyer country', facts.buyer.country],
    ],
    Bank: [
      ['/bank/iban', 'IBAN', facts.bank.iban],
      ['/bank/bic', 'BIC', facts.bank.bic],
    ],
    Lines: facts.lines.flatMap((line, index) => {
      const field = `/lines/${index}`
      const name = `Line ${index + 1}`
      return [
        [`${field}/line_id`, `${name} · ID`, line.line_id],
        [`${field}/sku`, `${name} · SKU`, line.sku],
        [`${field}/description`, `${name} · Description`, line.description],
        [`${field}/quantity`, `${name} · Quantity`, line.quantity],
        [`${field}/unit`, `${name} · Unit`, line.unit],
        [
          `${field}/unit_net_price`,
          `${name} · Unit net price`,
          line.unit_net_price,
        ],
        [`${field}/tax_rate`, `${name} · Tax rate`, line.tax_rate],
        [
          `${field}/declared_net_amount`,
          `${name} · Declared net`,
          line.declared_net_amount,
        ],
        [
          `${field}/declared_tax_amount`,
          `${name} · Declared tax`,
          line.declared_tax_amount,
        ],
      ] satisfies FactRow[]
    }),
    Totals: [
      ['/declared_subtotal', 'Declared subtotal', facts.declared_subtotal],
      ['/declared_tax_total', 'Declared tax total', facts.declared_tax_total],
      ['/declared_total', 'Declared total', facts.declared_total],
    ],
  }
})

const editableGroups = computed(() =>
  Object.fromEntries(
    Object.entries(groups.value).map(([group, rows]) => [
      group,
      rows.map(([path, label, value]) => ({
        path,
        label,
        value,
        field: editable.value.get(path),
      })),
    ]),
  ),
)

const citations = computed(() => {
  const byPath = new Map<string, InvoiceFacts['evidence']>()
  for (const quote of props.facts.evidence) {
    const quotes = byPath.get(quote.field_path)
    if (quotes) quotes.push(quote)
    else byPath.set(quote.field_path, [quote])
  }
  return byPath
})
const missing = computed(
  () =>
    new Map(
      props.facts.missing_fields.map((field) => [
        field.field_path,
        field.reason === 'illegible'
          ? 'Illegible in the PDF'
          : 'Not printed in the PDF',
      ]),
    ),
)
</script>

<template>
  <section class="card" aria-labelledby="read-facts">
    <div class="section-heading">
      <h2 id="read-facts">Facts read from the PDF</h2>
      <a v-if="pdf" :href="pdf" target="_blank" rel="noopener"
        >Open invoice PDF ↗</a
      >
    </div>
    <details
      v-for="(rows, group) in editableGroups"
      :key="group"
      class="fact-group"
      :open="group === 'Document'"
    >
      <summary>{{ group }}</summary>
      <div class="table-scroll">
        <table class="fact-table">
          <thead>
            <tr>
              <th scope="col">Fact</th>
              <th scope="col">Parsed value</th>
              <th scope="col">PDF quote</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="{ path, label, value, field } in rows" :key="path">
              <th scope="row">{{ label }}</th>
              <td :class="value === null ? 'muted' : 'mono'">
                {{ value ?? missing.get(path) ?? 'No value recorded' }}
              </td>
              <td>
                <InvoiceFactSource
                  :case-id="caseId"
                  :revision="revision"
                  :label="label"
                  :value="value"
                  :quotes="citations.get(path)"
                  :field="field"
                  :editing="editing === path"
                  @edit="editing = path"
                  @cancel="editing = ''"
                  @refresh="refresh"
                />
              </td>
            </tr>
          </tbody>
        </table>
      </div>
    </details>
  </section>
</template>
