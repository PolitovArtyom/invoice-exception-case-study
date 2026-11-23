<script setup lang="ts">
import { computed } from 'vue'
import type {
  InvoiceFacts,
  ProposalLineMatch,
  PurchaseOrder,
  ReceiptDirectory,
} from '../api'
import { formatDecimal } from '../formatDecimal'

const props = defineProps<{
  link: ProposalLineMatch
  facts: InvoiceFacts
  order: PurchaseOrder | null
  receiptDirectory: ReceiptDirectory
}>()

const invoice = computed(() =>
  props.facts.lines.find(
    (line) => line.line_id === props.link.document_line_id,
  ),
)
const orderLine = computed(() =>
  props.order?.lines.find((line) => line.line_id === props.link.order_line_id),
)
const receipts = computed(() =>
  props.receiptDirectory.receipts.filter(
    (receipt) =>
      receipt.order_id === props.order?.order_id &&
      receipt.order_line_id === props.link.order_line_id,
  ),
)
</script>

<template>
  <div class="proposal-mapping">
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
      <template v-if="orderLine">
        <p>
          {{ orderLine.description }} · Ordered
          {{ formatDecimal(orderLine.quantity) }} {{ orderLine.unit }} at
          {{ formatDecimal(orderLine.unit_net_price) }}
          {{ order?.currency }} per {{ orderLine.unit }}
        </p>
        <p v-if="receiptDirectory.availability === 'unavailable'">
          Receipt records unavailable
        </p>
        <p v-else-if="!receipts.length">No accepted receipts</p>
        <template v-else>
          <p>Accepted receipts</p>
          <ul>
            <li v-for="receipt in receipts" :key="receipt.receipt_id">
              {{ receipt.receipt_id }} ·
              {{ formatDecimal(receipt.accepted_quantity) }}
              {{ receipt.unit }} · {{ receipt.accepted_at }}
            </li>
          </ul>
        </template>
      </template>
    </div>
  </div>
</template>
