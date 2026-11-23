<script setup lang="ts">
import type { RevisionView } from '../api'
import DecisionTag from './DecisionTag.vue'

defineProps<{ caseId: string; selected: number; revisions: RevisionView[] }>()

const actions: Record<RevisionView['action']['kind'], string> = {
  load_case: 'Loaded case',
  register_invoice: 'Registered invoice',
  submit_case_inputs: 'Submitted case inputs',
  propose_match: 'Proposed a match',
  confirm_proposal: 'Confirmed a proposal',
  replace_company_data: 'Loaded company data',
  recheck_invoice: 'Re-checked invoice',
  correct_fact: 'Corrected a parsed value',
}
const actors: Record<RevisionView['action']['actor'], string> = {
  trusted_loader: 'Trusted loader',
  agent: 'Agent',
  human: 'Person',
  runner: 'Runner',
}

function actionLabel(action: RevisionView['action']) {
  return action.kind === 'recheck_invoice' && action.company_data_submitted
    ? 'Re-checked invoice with updated company data'
    : actions[action.kind]
}
</script>

<template>
  <section class="card" aria-labelledby="history-heading">
    <h2 id="history-heading">History</h2>
    <ol class="revision-history">
      <li
        v-for="(revision, index) in revisions"
        :key="revision.number"
        :class="{ 'selected-revision': revision.number === selected }"
      >
        <RouterLink
          :to="{
            name: 'revision',
            params: { caseId, revision: String(revision.number) },
          }"
          :aria-current="revision.number === selected ? 'page' : undefined"
          class="mono"
        >
          Revision {{ revision.number }}
        </RouterLink>
        <div>
          <p>
            <strong>{{ actionLabel(revision.action) }}</strong> ·
            {{ actors[revision.action.actor] }}
          </p>
          <p v-if="revision.action.proposal_ids?.length" class="muted">
            {{ revision.action.proposal_ids.join(', ') }}
          </p>
          <p
            v-for="(confirmation, position) in revision.action.confirmations ??
            []"
            :key="position"
          >
            {{ confirmation.confirmed_by }} confirmed
            {{ confirmation.proposal_id }}
          </p>
          <p v-if="revision.correction">
            {{ revision.correction.person }} corrected
            <code>{{ revision.correction.field_path }}</code>
            from {{ revision.correction.previous_value ?? 'missing' }} to
            {{ revision.correction.value ?? 'missing' }}.
            <q>{{ revision.correction.reason }}</q>
            <time :datetime="revision.action.recorded_at">
              {{ new Date(revision.action.recorded_at).toLocaleString() }}
            </time>
          </p>
          <p v-if="revision.acceptance">
            {{ revision.acceptance.person }} accepted the discrepancy in
            assessment {{ revision.acceptance.assessment_id }}.
            <q>{{ revision.acceptance.reason }}</q>
            <time :datetime="revision.acceptance.recorded_at">
              {{ new Date(revision.acceptance.recorded_at).toLocaleString() }}
            </time>
          </p>
          <p class="muted">
            Policy {{ revision.policy_version }} · Company data
            {{ revision.context_version }} · Document
            {{
              revision.duplicate_attempt?.reading.facts.document_id ??
              revision.document?.facts.document_id ??
              '—'
            }}
          </p>
          <p
            v-if="
              revisions[index + 1] &&
              revision.context_version !== revisions[index + 1]?.context_version
            "
            class="muted"
          >
            Company data changed from
            {{ revisions[index + 1]?.context_version }}
          </p>
          <p
            v-else-if="
              index < revisions.length - 1 &&
              revision.action.company_data_submitted
            "
            class="muted"
          >
            Company data submitted
          </p>
        </div>
        <DecisionTag
          v-if="revision.result"
          :decision="revision.result.decision"
        />
        <span v-else-if="revision.is_duplicate_attempt" class="muted"
          >Duplicate</span
        >
        <span v-else class="muted">No assessment</span>
      </li>
    </ol>
  </section>
</template>
