<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { RouterLink } from 'vue-router'
import { documentPdf, getCase, type CaseView } from '../api'
import ChecksTable from '../components/ChecksTable.vue'
import DecisionTag from '../components/DecisionTag.vue'
import InvoiceTotals from '../components/InvoiceTotals.vue'
import SourceTag from '../components/SourceTag.vue'
import { formatMoney } from '../money'

const props = defineProps<{ caseId: string }>()
const view = ref<CaseView | null>(null)
const error = ref('')
const document = computed(() => view.value?.revision.document)
const facts = computed(() => document.value?.facts)
const result = computed(() => view.value?.revision.result)

onMounted(async () => {
  try {
    view.value = await getCase(props.caseId)
  } catch (failure) {
    error.value = (failure as Error).message
  }
})
</script>

<template>
  <nav class="breadcrumb" aria-label="Breadcrumb">
    <RouterLink to="/">← Back to Cases</RouterLink>
    <span class="mono">{{ caseId }}</span>
    <span v-if="view?.case.origin === 'own'">Own scenario</span>
  </nav>
  <p v-if="error" role="alert">{{ error }}</p>
  <p v-else-if="view === null" role="status">Loading case…</p>
  <template v-else>
    <section class="card case-header" aria-labelledby="invoice-title">
      <div>
        <h1 id="invoice-title">{{ facts?.number ?? caseId }}</h1>
        <template v-if="facts">
          <p class="parties">
            {{ facts.supplier.legal_name ?? 'Supplier unavailable' }}
            <span aria-label="to">→</span>
            {{ facts.buyer.legal_name ?? 'Buyer unavailable' }}
          </p>
          <div class="invoice-dates">
            <span
              >Issued
              <time :datetime="facts.issue_date">{{
                facts.issue_date
              }}</time></span
            >
            <span
              >Due
              <time v-if="facts.due_date" :datetime="facts.due_date">{{
                facts.due_date
              }}</time><template v-else>—</template></span
            >
          </div>
          <dl class="references">
            <div>
              <dt>Printed PO</dt>
              <dd class="mono">
                {{ facts.purchase_order_reference ?? 'Not printed' }}
              </dd>
            </div>
            <div v-if="result?.matched_order_id">
              <dt>Matched order</dt>
              <dd class="mono">{{ result.matched_order_id }}</dd>
            </div>
          </dl>
        </template>
      </div>
      <div class="invoice-decision">
        <p v-if="facts" class="declared">
          <span class="number"
            >{{ formatMoney(facts.declared_total) }} {{ facts.currency }}</span
          >
          <SourceTag source="invoice" />
        </p>
        <DecisionTag :decision="result?.decision ?? null" />
        <p v-if="result?.decision === 'clear'" class="stage">
          Ready for accounting
        </p>
      </div>
      <div class="invoice-meta">
        <span
          >Revision {{ view.revision.number }} ·
          <span class="mono">{{ view.revision.context_version }}</span></span
        >
        <span v-if="document">{{ document.reading_origin }}</span>
        <a
          v-if="document?.source"
          :href="documentPdf(caseId, document.reference)"
          target="_blank"
          rel="noopener"
          >Open invoice PDF ↗</a
        >
      </div>
    </section>
    <template v-if="result">
      <InvoiceTotals :totals="result.totals" :currency="facts?.currency" />
      <ChecksTable :checks="result.checks" />
    </template>
    <p v-else class="waiting-note">The agent has not read this invoice yet.</p>
  </template>
</template>
