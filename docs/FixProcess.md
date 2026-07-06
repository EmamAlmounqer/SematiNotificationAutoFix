# Fix Process

## No Notification Action For action

### Get Msisdn From Activation

Check Identity master, filter by person id
Check Activation by Identity Master, Get Msisdn And Nobill Account Number 

### Get Msisdn From Semati Call Report

from semati call report get all success call by person id
get all msisdn and remove the success `TerminateActivation`
remove the msisdn that transfer the owner:
    group by msisdn:
        if last success (`TerminateActivation`) => ignore
        if last success (`NewActivation`) => should be terminate + add action
        if last success (`TransferOwner`) => skip for now
        other skip for now 