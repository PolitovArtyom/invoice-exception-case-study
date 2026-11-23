<script setup lang="ts">
import { ref, watch } from 'vue'
import Button from 'primevue/button'
import { ActionError, recheckInvoice } from '../api'

const props = defineProps<{ caseId: string }>()
const emit = defineEmits<{ rechecked: [] }>()

const loading = ref(false)
const status = ref<{ text: string; role: 'status' | 'alert' } | null>(null)

watch(status, (value, _, onCleanup) => {
  if (!value) return
  const timer = globalThis.setTimeout(() => (status.value = null), 5000)
  onCleanup(() => globalThis.clearTimeout(timer))
})

async function recheck() {
  loading.value = true
  status.value = null
  try {
    const { inputs_unchanged, result } = await recheckInvoice(props.caseId)
    status.value = {
      role: 'status',
      text: inputs_unchanged
        ? 'Nothing changed'
        : `New assessment saved — revision ${result.input_revision}`,
    }
    emit('rechecked')
  } catch (failure) {
    status.value = {
      role: 'alert',
      text:
        failure instanceof ActionError && failure.field
          ? `${failure.field} — ${failure.message}`
          : (failure as Error).message,
    }
  } finally {
    loading.value = false
  }
}
</script>

<template>
  <div class="invoice-recheck">
    <Transition name="recheck-status">
      <span
        v-if="status"
        :role="status.role"
        :class="{ 'recheck-error': status.role === 'alert' }"
        >{{ status.text }}</span
      >
    </Transition>
    <Button
      type="button"
      label="Re-check"
      title="Load the latest company data and assess again"
      severity="secondary"
      size="small"
      outlined
      :loading="loading"
      @click="recheck"
    >
      <template #icon
        ><span class="refresh-icon" aria-hidden="true">↻</span></template
      >
    </Button>
  </div>
</template>

<style scoped>
.invoice-recheck {
  display: flex;
  align-items: center;
  gap: 0.6rem;
  max-width: 100%;
  margin-inline-start: auto;
}

.invoice-recheck button {
  flex-shrink: 0;
}

.invoice-recheck > span {
  text-align: end;
  overflow-wrap: anywhere;
}

.refresh-icon {
  font-size: 1.15em;
  line-height: 1;
}

.recheck-error {
  color: #8a2c19;
}

.recheck-status-leave-active {
  transition: opacity 250ms ease;
}

.recheck-status-leave-to {
  opacity: 0;
}
</style>
