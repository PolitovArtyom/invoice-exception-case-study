<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { RouterLink, useRouter } from 'vue-router'
import { listCases } from '../api'
import DecisionTag from '../components/DecisionTag.vue'
import SourceTag from '../components/SourceTag.vue'
import { caseStageLabel, simulatedHandover, type CaseTab } from '../caseStage'
import { checkTitle, issueOwnerLabel } from '../checkPresentation'
import { formatDecimal } from '../formatDecimal'

const router = useRouter()
const list = ref<Awaited<ReturnType<typeof listCases>> | null>(null)
const error = ref('')
const tabs: CaseTab[] = ['needs_attention', 'ready_for_accounting', 'closed']
const filter = ref<CaseTab>('needs_attention')
const visibleCases = computed(
  () => list.value?.cases.filter((item) => item.tab === filter.value) ?? [],
)

onMounted(async () => {
  try {
    list.value = await listCases()
  } catch (failure) {
    error.value = (failure as Error).message
  }
})
</script>

<template>
  <h1>Cases</h1>
  <p v-if="error" role="alert">{{ error }}</p>
  <p v-else-if="list === null" role="status">Loading cases…</p>
  <template v-else>
    <div class="case-filters" role="group" aria-label="Filter cases">
      <button
        v-for="tab in tabs"
        :key="tab"
        type="button"
        :aria-pressed="filter === tab"
        @click="filter = tab"
      >
        {{ caseStageLabel(tab) }} ({{ list.counts[tab] }})
      </button>
    </div>
    <section class="card table-card" aria-label="Invoice cases">
      <div v-if="visibleCases.length" class="table-scroll">
        <table class="case-list">
          <thead>
            <tr>
              <th scope="col">Invoice</th>
              <th scope="col">Supplier</th>
              <th scope="col">Issue date</th>
              <th scope="col" class="align-right">Declared total</th>
              <th scope="col">Result</th>
              <th scope="col">Summary</th>
            </tr>
          </thead>
          <tbody>
            <tr
              v-for="item in visibleCases"
              :key="item.case_id"
              @click="
                router.push({ name: 'case', params: { caseId: item.case_id } })
              "
            >
              <td>
                <RouterLink
                  :to="{ name: 'case', params: { caseId: item.case_id } }"
                  @click.stop
                >
                  {{ item.number ?? item.case_id }}
                </RouterLink>
                <small v-if="item.number" class="mono">{{
                  item.case_id
                }}</small>
                <small v-if="item.origin === 'own'">Own scenario</small>
              </td>
              <td>{{ item.supplier ?? '—' }}</td>
              <td>
                <time v-if="item.issue_date" :datetime="item.issue_date">
                  {{ item.issue_date }}
                </time>
                <template v-else>—</template>
              </td>
              <td class="align-right amount-cell">
                <template v-if="item.declared_total !== null">
                  <span class="number"
                    >{{ formatDecimal(item.declared_total) }}
                    {{ item.currency }}</span
                  >
                  <small><SourceTag source="invoice" /></small>
                </template>
                <template v-else>—</template>
              </td>
              <td>
                <DecisionTag :decision="item.decision" :stage="item.stage" />
              </td>
              <td>
                <template v-if="item.stage === 'ready_for_accounting'">
                  {{ simulatedHandover }}
                  <small>
                    Revision {{ item.assessed_revision }} ·
                    {{ item.context_version }}
                  </small>
                </template>
                <template v-else-if="item.stage === 'closed'">
                  Already registered as {{ item.original_document_id }}.
                  <small>No further processing.</small>
                </template>
                <template v-else-if="item.main_issue">
                  {{ checkTitle(item.main_issue) }}
                  <small v-if="item.main_issue_owner">
                    Ask {{ issueOwnerLabel(item.main_issue_owner) }}.
                  </small>
                </template>
                <template v-else-if="item.stage === 'needs_attention'">
                  Re-check with the latest company data.
                </template>
                <template v-else>{{ caseStageLabel(item.stage) }}</template>
              </td>
            </tr>
          </tbody>
        </table>
      </div>
      <p v-else class="empty-list">No cases in {{ caseStageLabel(filter) }}.</p>
    </section>
  </template>
</template>
