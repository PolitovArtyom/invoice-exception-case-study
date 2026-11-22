<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { RouterLink, useRouter } from 'vue-router'
import {
  documentPdf,
  getCase,
  getHistory,
  revisionPdf,
  type CaseView,
  type ReadingOrigin,
  type RevisionView,
} from '../api'
import CaseHistory from '../components/CaseHistory.vue'
import ProposalReview from '../components/ProposalReview.vue'
import ConflictDifferences from '../components/ConflictDifferences.vue'
import InvoiceFactsPanel from '../components/InvoiceFactsPanel.vue'
import ChecksTable from '../components/ChecksTable.vue'
import DecisionTag from '../components/DecisionTag.vue'
import InvoiceLineCard from '../components/InvoiceLineCard.vue'
import NeedsAttention from '../components/NeedsAttention.vue'
import InvoiceTotals from '../components/InvoiceTotals.vue'
import SourceTag from '../components/SourceTag.vue'
import { isLineCheck } from '../checkPresentation'
import { caseStageLabel } from '../caseStage'
import { formatDecimal } from '../formatDecimal'

const readingOriginLabels: Record<ReadingOrigin, string> = {
  fixture: 'loaded from test data, not read by the agent',
  agent: 'read by the agent',
  runner: 'loaded by the runner',
}

const props = defineProps<{ caseId: string; revision?: string }>()
const router = useRouter()
let nextRevisionMessage = ''
const view = ref<CaseView | null>(null)
const error = ref('')
const actionMessage = ref('')
const history = ref<RevisionView[]>([])
const document = computed(() => view.value?.revision.document)
const reading = computed(() =>
  view.value?.show_duplicate_reading
    ? view.value.revision.duplicate_attempt?.reading
    : document.value,
)
const facts = computed(() => reading.value?.facts)
const readingOrigin = computed(() => reading.value?.reading_origin)
const pdf = computed(() => {
  if (!view.value?.invoice_pdf_available) return undefined
  if (view.value.show_duplicate_reading)
    return revisionPdf(props.caseId, view.value.revision.number, 'duplicate')
  if (document.value?.source)
    return documentPdf(props.caseId, document.value.reference)
  return undefined
})
const original = computed(() => view.value?.revision.original_invoice)
const originalPdf = computed(() =>
  view.value?.original_pdf_available
    ? revisionPdf(props.caseId, view.value.revision.number, 'original')
    : undefined,
)
const result = computed(() => view.value?.revision.result)

const totalsCheck = computed(() =>
  result.value?.checks.find((check) => check.code === 'TOTALS_ARITHMETIC'),
)
const lineCards = computed(() =>
  (facts.value?.lines ?? []).flatMap((line) => {
    const assessment = result.value?.lines.find(
      (item) => item.document_line_id === line.line_id,
    )
    if (!assessment) return []
    const checks = (result.value?.checks ?? []).filter(
      (check) => check.object_id === line.line_id && isLineCheck(check),
    )
    return [{ line, assessment, checks }]
  }),
)
async function load(message = '') {
  const caseId = props.caseId
  const revision = props.revision
  error.value = ''
  actionMessage.value = ''
  try {
    const [page, revisions] = await Promise.all([
      getCase(caseId, revision),
      getHistory(caseId),
    ])
    if (props.caseId !== caseId || props.revision !== revision) return
    view.value = page
    history.value = revisions
    actionMessage.value = message
  } catch (failure) {
    if (props.caseId === caseId && props.revision === revision)
      error.value = (failure as Error).message
  }
}

async function refreshAfterProposal(message = '') {
  if (!props.revision) {
    await load(message)
    return
  }

  nextRevisionMessage = message
  await router.replace({ name: 'case', params: { caseId: props.caseId } })
}

watch(
  () => [props.caseId, props.revision],
  () => {
    view.value = null
    const message = nextRevisionMessage
    nextRevisionMessage = ''
    void load(message)
  },
  { immediate: true },
)
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
    <p v-if="actionMessage" role="status" class="card">{{ actionMessage }}</p>
    <p v-if="view.superseded" class="card">
      Revision {{ view.revision.number }} has been superseded.
      <RouterLink :to="{ name: 'case', params: { caseId } }"
        >Open the latest</RouterLink
      >
    </p>
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
              <dt>
                {{
                  result.order_match_source === 'agent_proposal'
                    ? 'Proposed order'
                    : 'Matched order'
                }}
              </dt>
              <dd class="mono">{{ result.matched_order_id }}</dd>
            </div>
          </dl>
        </template>
      </div>
      <div class="invoice-decision">
        <p v-if="facts" class="declared">
          <span class="number"
            >{{ formatDecimal(facts.declared_total) }}
            {{ facts.currency }}</span
          >
          <SourceTag source="invoice" />
        </p>
        <DecisionTag
          :decision="
            view.show_duplicate_reading
              ? 'duplicate'
              : (result?.decision ?? null)
          "
          :stage="view.stage"
        />
        <p
          v-if="
            view.stage === 'ready_for_accounting' || view.stage === 'closed'
          "
          class="stage"
        >
          {{ caseStageLabel(view.stage) }}
        </p>
      </div>
      <div class="invoice-meta">
        <span
          >Revision {{ view.revision.number }} ·
          <span class="mono">{{ view.revision.context_version }}</span></span
        >
        <span v-if="readingOrigin">{{
          readingOriginLabels[readingOrigin]
        }}</span>
        <a v-if="pdf" :href="pdf" target="_blank" rel="noopener"
          >Open invoice PDF ↗</a
        >
        <span v-else-if="facts" class="muted">Invoice PDF unavailable</span>
      </div>
    </section>
    <section
      v-if="view.show_duplicate_reading && original"
      class="card"
      aria-labelledby="duplicate-heading"
    >
      <h2 id="duplicate-heading">
        {{
          view.stage === 'closed' ? 'Closed as duplicate' : 'Duplicate attempt'
        }}
      </h2>
      <p>
        Resend of
        <a
          v-if="originalPdf"
          :href="originalPdf"
          target="_blank"
          rel="noopener"
          >{{ original.document_id }}</a
        >
        <span v-else class="mono">{{ original.document_id }}</span>. This resend
        requires no further processing.
      </p>
    </section>
    <p v-if="view.latest_duplicate_revision !== null" class="card">
      Duplicate received at revision {{ view.latest_duplicate_revision }}
    </p>
    <p v-if="view.inputs_changed && !view.superseded" class="card">
      Case inputs changed after this assessment. A new assessment is required
      before accounting.
    </p>
    <template v-if="result && facts">
      <NeedsAttention
        :checks="result.checks"
        :lines="facts.lines"
        :currency="facts.currency"
      />
      <ProposalReview
        v-if="view.proposals.length"
        :key="view.current_revision + '/' + view.revision.number"
        :view="view"
        :facts="facts"
        @refresh="refreshAfterProposal"
      />
      <ConflictDifferences
        v-if="original"
        :checks="result.checks"
        :original-id="original.document_id"
        :pdf="originalPdf"
      />
      <section
        v-if="lineCards.length"
        class="invoice-lines"
        aria-labelledby="invoice-lines-heading"
      >
        <h2 id="invoice-lines-heading">Invoice lines</h2>
        <InvoiceLineCard
          v-for="{ line, assessment, checks } in lineCards"
          :key="line.line_id"
          :line="line"
          :assessment="assessment"
          :checks="checks"
          :facts="facts"
          :confirmed-by="
            view.proposals.find(
              (item) =>
                item.registration.proposal.proposal_id ===
                view?.effective_proposal_id,
            )?.registration.confirmation?.confirmed_by
          "
        />
      </section>
      <InvoiceTotals
        v-if="totalsCheck"
        :currency="facts.currency"
        :check="totalsCheck"
      />
      <ChecksTable :checks="result.checks" :facts="facts" />
    </template>
    <InvoiceFactsPanel v-if="facts" :facts="facts" :pdf="pdf" />
    <p v-else class="waiting-note">The agent has not read this invoice yet.</p>
    <CaseHistory
      :case-id="caseId"
      :selected="view.revision.number"
      :revisions="history"
    />
  </template>
</template>
