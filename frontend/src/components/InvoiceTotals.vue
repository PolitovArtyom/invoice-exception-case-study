<script setup lang="ts">
import type { InvoiceResult } from '../api'
import SourceTag from './SourceTag.vue'
import { formatMoney } from '../money'

defineProps<{
  totals: InvoiceResult['totals']
  currency: string | null | undefined
}>()
</script>

<template>
  <section class="card" aria-labelledby="totals-title">
    <h2 id="totals-title">Calculated totals</h2>
    <dl class="totals-grid">
      <div>
        <dt>Net subtotal</dt>
        <dd>
          <strong class="number"
            >{{ formatMoney(totals.calculated_subtotal) }}
            {{ currency }}</strong
          >
          <SourceTag source="calculated" />
        </dd>
      </div>
      <div>
        <dt>Tax</dt>
        <dd>
          <strong class="number"
            >{{ formatMoney(totals.calculated_tax_total) }}
            {{ currency }}</strong
          >
          <SourceTag source="calculated" />
        </dd>
      </div>
      <div>
        <dt>Invoice total</dt>
        <dd>
          <strong class="number"
            >{{ formatMoney(totals.calculated_total) }} {{ currency }}</strong
          >
          <SourceTag source="calculated" />
        </dd>
      </div>
    </dl>
  </section>
</template>
