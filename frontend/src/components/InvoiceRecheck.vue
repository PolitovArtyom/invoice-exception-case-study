<script setup lang="ts">
import { ref } from 'vue'
import Button from 'primevue/button'
import { ActionError, recheckInvoice } from '../api'

const props = defineProps<{ caseId: string }>()
const emit = defineEmits<{ rechecked: [] }>()

const loading = ref(false)
const error = ref('')
const message = ref('')

async function recheck() {
  loading.value = true
  error.value = ''
  message.value = ''
  try {
    const { inputs_unchanged, result } = await recheckInvoice(props.caseId)
    message.value = inputs_unchanged
      ? 'Nothing changed. The saved assessment is still current.'
      : `Re-check saved revision ${result.input_revision} with company data ${result.context_version}.`
    emit('rechecked')
  } catch (failure) {
    error.value =
      failure instanceof ActionError && failure.field
        ? `${failure.field} — ${failure.message}`
        : (failure as Error).message
  } finally {
    loading.value = false
  }
}
</script>

<template>
  <div>
    <Button
      type="button"
      label="Re-check with the latest company data"
      :loading="loading"
      @click="recheck"
    />
    <p v-if="error" role="alert">{{ error }}</p>
    <p v-if="message" role="status">{{ message }}</p>
  </div>
</template>
