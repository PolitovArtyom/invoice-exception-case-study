<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { RouterLink, useRouter } from 'vue-router'
import { listCases, type CaseSummary } from '../api'
import DecisionTag from '../components/DecisionTag.vue'
import SourceTag from '../components/SourceTag.vue'
import { caseStageLabel } from '../caseStage'
import { formatDecimal } from '../formatDecimal'

const router = useRouter()
const cases = ref<CaseSummary[] | null>(null)
const error = ref('')

onMounted(async () => {
  try {
    cases.value = await listCases()
  } catch (failure) {
    error.value = (failure as Error).message
  }
})
</script>

<template>
  <h1>Cases</h1>
  <p v-if="error" role="alert">{{ error }}</p>
  <p v-else-if="cases === null" role="status">Loading cases…</p>
  <section v-else class="card table-card" aria-label="Invoice cases">
    <div v-if="cases.length" class="table-scroll">
      <table class="case-list">
        <thead>
          <tr>
            <th scope="col">Invoice</th>
            <th scope="col">Supplier</th>
            <th scope="col" class="align-right">Declared total</th>
            <th scope="col">Result</th>
            <th scope="col">Stage</th>
          </tr>
        </thead>
        <tbody>
          <tr
            v-for="item in cases"
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
              <small v-if="item.number || item.origin === 'own'" class="mono">
                {{ item.number ? item.case_id : ''
                }}{{ item.origin === 'own' ? ' · Own scenario' : '' }}
              </small>
            </td>
            <td>{{ item.supplier ?? '—' }}</td>
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
            <td><DecisionTag :decision="item.decision" /></td>
            <td>
              {{ caseStageLabel(item.stage) }}
            </td>
          </tr>
        </tbody>
      </table>
    </div>
    <p v-else class="empty-list">No cases loaded.</p>
  </section>
</template>
