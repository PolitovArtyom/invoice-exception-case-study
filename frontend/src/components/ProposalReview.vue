<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import {
  ActionError,
  confirmProposal,
  previewConfirmation,
  type CaseView,
  type InvoiceFacts,
  type ProposalView,
} from '../api'
import { checkTitle } from '../checkPresentation'
import { formatDecimal } from '../formatDecimal'
import DecisionTag from './DecisionTag.vue'

const props = defineProps<{ view: CaseView; facts: InvoiceFacts }>()
const emit = defineEmits<{ refresh: [message?: string] }>()
const staleMessage =
  'The case changed. The latest revision is shown. Review the proposal before confirming.'
const selectedId = ref(
  props.view.effective_proposal_id ??
    props.view.proposals[0]?.registration.proposal.proposal_id,
)
const selected = computed(() =>
  props.view.proposals.find(
    ({ registration }) =>
      registration.proposal.proposal_id === selectedId.value,
  ),
)
const preview = ref<Awaited<ReturnType<typeof previewConfirmation>> | null>(
  null,
)
const busy = ref(false)
const error = ref('')
const name = ref('')
const reviewed = ref(false)

watch(
  selectedId,
  async (id, _, onCleanup) => {
    preview.value = null
    name.value = ''
    reviewed.value = false
    error.value = ''
    if (!id || !selected.value?.can_preview) return

    const request = new globalThis.AbortController()
    onCleanup(() => request.abort())
    try {
      const response = await previewConfirmation(
        props.view.case.case_id,
        id,
        request.signal,
      )
      if (request.signal.aborted) return
      if (response.expected_revision !== props.view.current_revision) {
        emit('refresh', staleMessage)
        return
      }
      preview.value = response
    } catch (failure) {
      if (!request.signal.aborted) error.value = (failure as Error).message
    }
  },
  { immediate: true },
)

const mapping = computed(
  () =>
    selected.value?.registration.proposal.line_matches.map((link) => ({
      link,
      invoice: props.facts.lines.find(
        (line) => line.line_id === link.document_line_id,
      ),
      order: selected.value?.order?.lines.find(
        (line) => line.line_id === link.order_line_id,
      ),
    })) ?? [],
)
const automatic = computed(() => {
  const result = preview.value?.result ?? props.view.revision.result
  if (!result || result.matched_order_id !== selected.value?.order?.order_id)
    return []
  return result.lines.filter((line) => line.match_source === 'deterministic')
})
const alternatives = computed(() =>
  props.view.open_supplier_orders.filter(
    (order) => order.order_id !== selected.value?.order?.order_id,
  ),
)
const remainingChecks = computed(
  () =>
    preview.value?.result.checks.filter(
      (check) =>
        check.status === 'fail' ||
        (check.status === 'unknown' && check.depends_on === null),
    ) ?? [],
)

function proposalLabel({ order, supplier_name, registration }: ProposalView) {
  if (!order) return registration.proposal.order_id

  const label = `${order.reference} · ${supplier_name ?? order.supplier_id}`
  const sameOrder = props.view.proposals.filter(
    (item) => item.order?.order_id === order.order_id,
  )
  if (sameOrder.length === 1) return label

  const mapping = registration.proposal.line_matches
    .map((line) => `${line.document_line_id} → ${line.order_line_id}`)
    .join(', ')
  return `${label} · ${mapping || 'No line links'}`
}

async function confirm() {
  if (
    !selected.value?.can_confirm ||
    !preview.value ||
    !name.value.trim() ||
    !reviewed.value ||
    busy.value
  )
    return
  busy.value = true
  error.value = ''
  try {
    await confirmProposal(
      props.view.case.case_id,
      preview.value,
      name.value,
      reviewed.value,
    )
    emit('refresh')
  } catch (failure) {
    const message = (failure as Error).message
    if (
      failure instanceof ActionError &&
      failure.field === '/expected_revision'
    )
      emit('refresh', staleMessage)
    else error.value = message
  } finally {
    busy.value = false
  }
}
</script>

<template>
  <section
    v-if="selected"
    class="card proposal-card"
    aria-labelledby="proposal-heading"
  >
    <h2 id="proposal-heading">Agent proposal</h2>
    <div class="proposal-status">
      <label v-if="view.proposals.length > 1" for="proposal-choice"
        >Proposal</label
      >
      <select
        v-if="view.proposals.length > 1"
        id="proposal-choice"
        v-model="selectedId"
        :disabled="busy"
      >
        <option
          v-for="proposal in view.proposals"
          :key="proposal.registration.proposal.proposal_id"
          :value="proposal.registration.proposal.proposal_id"
        >
          {{ proposalLabel(proposal) }}
        </option>
      </select>
      <span v-else class="mono">{{
        selected.registration.proposal.proposal_id
      }}</span>
      <strong v-if="selected.registration.confirmation">
        Confirmed by {{ selected.registration.confirmation.confirmed_by }}
      </strong>
      <span v-else-if="selected.can_confirm">Awaiting confirmation</span>
      <span v-if="!selected.applicable" class="muted"
        >Unavailable with this invoice or company data</span
      >
    </div>
    <dl class="references">
      <div>
        <dt>Printed PO</dt>
        <dd>{{ facts.purchase_order_reference ?? 'Not printed' }}</dd>
      </div>
      <div>
        <dt>Proposed order</dt>
        <dd class="mono">
          {{ selected.registration.proposal.order_id
          }}<template v-if="selected.order">
            · {{ selected.order.reference }}</template
          >
        </dd>
      </div>
    </dl>
    <div
      v-for="{ link, invoice, order } in mapping"
      :key="link.document_line_id"
      class="proposal-mapping"
    >
      <div>
        <strong>Invoice line {{ link.document_line_id }}</strong>
        <p v-if="invoice">
          {{ invoice.description }} · {{ formatDecimal(invoice.quantity) }}
          {{ invoice.unit }} × {{ formatDecimal(invoice.unit_net_price) }}
          {{ facts.currency }}
        </p>
      </div>
      <div>
        <strong>Order line {{ link.order_line_id }}</strong>
        <p v-if="order">
          {{ order.description }} · {{ formatDecimal(order.quantity) }}
          {{ order.unit }} × {{ formatDecimal(order.unit_net_price) }}
          {{ selected.order?.currency }}
        </p>
      </div>
    </div>
    <p v-if="automatic.length" class="muted">
      Matched automatically —
      {{
        automatic
          .map((line) => `${line.document_line_id} → ${line.order_line_id}`)
          .join(', ')
      }}
    </p>
    <p v-if="alternatives.length" class="muted">
      Other open orders for this supplier —
      {{
        alternatives
          .map((order) => `${order.order_id} (${order.reference})`)
          .join(', ')
      }}
    </p>
    <p v-if="error" role="alert">{{ error }}</p>
    <div v-if="selected.can_preview && preview" class="confirmation-review">
      <p class="confirmation-preview">
        If you confirm this proposal
        <DecisionTag :decision="preview.result.decision" />
      </p>
      <ul
        v-if="remainingChecks.length"
        aria-label="Checks still requiring attention"
      >
        <li
          v-for="check in remainingChecks"
          :key="`${check.code}/${check.object_id}`"
        >
          {{ checkTitle(check.code) }} · {{ check.object_id }}
        </li>
      </ul>
      <form
        v-if="selected.can_confirm"
        class="confirmation-form"
        @submit.prevent="confirm"
      >
        <label for="confirmed-by">Your name</label>
        <input
          id="confirmed-by"
          v-model="name"
          name="confirmed_by"
          maxlength="128"
          autocomplete="name"
          required
        />
        <label class="confirmation-checkbox">
          <input
            v-model="reviewed"
            name="supporting_documents_reviewed"
            type="checkbox"
            required
          />
          I checked the supporting delivery documents
        </label>
        <button type="submit" :disabled="!name.trim() || !reviewed || busy">
          Confirm this proposal
        </button>
      </form>
    </div>
    <p v-else-if="selected.can_preview && !preview && !error" role="status">
      Calculating confirmation preview…
    </p>
  </section>
</template>
